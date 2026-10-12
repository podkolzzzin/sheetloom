using ClosedXML.Excel;
using System.Globalization;

namespace Sheetloom;

internal sealed class ExcelCellRenderer
{
    private readonly List<(IXLCell Cell, IXLRow Row, CellModel CellModel)> pendingFitRows = [];
    private readonly ExcelImageRenderer images = new();

    public void CompleteLayout()
    {
        foreach (var (cell, row, model) in pendingFitRows)
        {
            ApplyFitRow(cell, row, model);
        }

        images.CompleteLayout();
    }

    public void RenderRow(
        ExcelFormulaRendererInstance formulaRenderer,
        IXLWorksheet excelWorksheet,
        RowModel row,
        IXLRow xlRow)
    {
        if (row.Style.HasValue)
        {
            ExcelStyleRenderer.Render(row.Style, xlRow.Style);
            if (row.Style.Value.TryGet<BreakStyle>(StyleKey.BreakAfter, out _))
            {
                xlRow.AddHorizontalPageBreak();
            }
        }

        ExcelStyleRenderer.ApplyHeight(row.Style, value => xlRow.Height = value);
        if (row.Hide)
        {
            xlRow.Hide();
        }

        int lastColumn = RenderCells(formulaRenderer, excelWorksheet, row, xlRow);
        ApplyAutoFilter(row, xlRow, lastColumn);
    }

    private int RenderCells(
        ExcelFormulaRendererInstance formulaRenderer,
        IXLWorksheet excelWorksheet,
        RowModel row,
        IXLRow xlRow)
    {
        int lastColumn = 0;
        for (int cellIndex = 0, modelIndex = 0; modelIndex < row.Cells.Length; cellIndex++)
        {
            var xlCell = xlRow.Cell(cellIndex + 1);
            if (xlCell.IsMerged())
            {
                continue;
            }

            var cell = row.Cells[modelIndex];
            if (cell.Colspan is > 1 || cell.Rowspan is > 1)
            {
                var candidate = excelWorksheet.Range(xlCell.Address.RowNumber, xlCell.Address.ColumnNumber,
                    xlCell.Address.RowNumber + Math.Max(cell.Rowspan ?? 1, 1) - 1,
                    xlCell.Address.ColumnNumber + Math.Max(cell.Colspan ?? 1, 1) - 1);
                if (excelWorksheet.MergedRanges.Any(range => range.Intersects(candidate)))
                {
                    continue;
                }
            }
            formulaRenderer.Track(cell, xlCell);
            if (!string.IsNullOrEmpty(cell.ExcelNamed))
            {
                excelWorksheet.DefinedNames.Add(cell.ExcelNamed, xlCell.AsRange());
            }

            // Apply merge first (before image so we know the full range)
            bool merged = ApplyMerge(excelWorksheet, cell, xlCell);
            lastColumn = Math.Max(lastColumn, merged
                ? xlCell.MergedRange().LastColumn().ColumnNumber()
                : xlCell.Address.ColumnNumber);

            string? literalText = null;
            if (!string.IsNullOrEmpty(cell.ImageSource))
            {
                images.Add(excelWorksheet, xlCell, cell);
                xlCell.SetValue(string.Empty);
            }
            else
            {
                literalText = ApplyValue(formulaRenderer, xlCell, cell);
            }

            if (cell.Style.HasValue)
            {
                ExcelStyleRenderer.Render(cell.Style, merged ? xlCell.MergedRange().Style : xlCell.Style);
                if (cell.Style.Value.TryGet<BreakStyle>(StyleKey.BreakAfter, out _))
                {
                    xlCell.Worksheet.PageSetup.AddVerticalPageBreak(xlCell.Address.ColumnNumber);
                }
            }

            // Apply text wrapping
            if (cell.WrapText)
            {
                xlCell.Style.Alignment.WrapText = true;
            }

            // ClosedXML's plain-value setter consumes a leading apostrophe. A literal rich-text
            // run preserves it, using the final effective font rather than quote-prefix metadata.
            if (literalText is not null)
            {
                xlCell.GetRichText().AddText(literalText);
            }

            ExcelStyleRenderer.ApplyWidth(cell.Style, value => xlCell.WorksheetColumn().Width = value);
            ExcelStyleRenderer.ApplyHeight(cell.Style, value => xlRow.Height = value);

            if (cell.FitColumn)
            {
                xlCell.WorksheetColumn().AdjustToContents();
            }

            if (cell.FitRow || xlCell.Style.Alignment.WrapText)
            {
                pendingFitRows.Add((xlCell, xlRow, cell));
            }

            modelIndex++;
        }
        return lastColumn;
    }

    private static void ApplyAutoFilter(RowModel row, IXLRow xlRow, int lastColumn)
    {
        if (!row.AutoFilter)
        {
            return;
        }

        int rowNumber = xlRow.RowNumber();
        // Multi-row headers can inherit trailing columns without emitting a cell in this row.
        // Only intersecting merges count; earlier titles must not widen the filter.
        foreach (var mergedRange in xlRow.Worksheet.MergedRanges)
        {
            var address = mergedRange.RangeAddress;
            if (address.FirstAddress.RowNumber <= rowNumber && address.LastAddress.RowNumber >= rowNumber)
                lastColumn = Math.Max(lastColumn, address.LastAddress.ColumnNumber);
        }

        if (lastColumn > 0)
            xlRow.Worksheet.Range(rowNumber, 1, rowNumber, lastColumn).SetAutoFilter();
    }

    private void ApplyFitRow(IXLCell xlCell, IXLRow xlRow, CellModel cell)
    {
        if (string.IsNullOrWhiteSpace(cell.Text))
        {
            return;
        }

        var targetRange = xlCell.IsMerged() ? xlCell.MergedRange() : xlCell.AsRange();
        var firstCol = targetRange.FirstColumn().ColumnNumber();
        var lastCol = targetRange.LastColumn().ColumnNumber();

        // Calculate total width in Excel units
        double totalWidth = 0;
        for (int col = firstCol; col <= lastCol; col++)
        {
            totalWidth += xlCell.Worksheet.Column(col).Width;
        }

        if (totalWidth <= 0)
        {
            return;
        }

        // Get font size from style (cell style or row style)
        double fontSize = cell.Style.HasValue && cell.Style.Value.TryGet<double>(StyleKey.FontSize, out var fs)
            ? fs
            : xlCell.Style.Font.FontSize;

        // Get font name from the already-applied Excel cell style
        string fontName = xlCell.Style.Font.FontName;
        double fontWidthFactor = GetFontCharWidthFactor(fontName);

        // Prepare text
        var text = ExcelStyleRenderer.PrepareText(cell.Text);
        var lines = text.Split('\n');

        // Estimate chars per line based on width, font size and font family.
        // Excel column width unit = width of '0' in the Normal style (Calibri 11pt).
        // fontWidthFactor corrects for fonts that are wider/narrower than Calibri.
        double charsPerLine = Math.Max(5.0, totalWidth * (11.0 / fontSize) * fontWidthFactor);

        // Count wrapped lines
        int totalLines = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                totalLines++;
            }
            else
            {
                totalLines += Math.Max(1, (int)Math.Ceiling(line.Length / charsPerLine));
            }
        }

        // Calculate height with line spacing
        double heightInPoints = totalLines * fontSize * 1.38;

        xlRow.Height = Math.Max(xlRow.Height, heightInPoints);
    }

    private string? ApplyValue(ExcelFormulaRendererInstance instance, IXLCell xlCell, CellModel cell)
    {
        if (cell.Formula != null)
        {
            var formula = instance.Render(cell.Formula);
            xlCell.FormulaR1C1 = formula;
        }
        else
        {
            var value = GetValue(cell);
            if (value.IsText && value.GetText().StartsWith("'", StringComparison.Ordinal))
            {
                return value.GetText();
            }
            xlCell.SetValue(value);
        }
        return null;
    }

    private static bool ApplyMerge(IXLWorksheet excelWorksheet, CellModel cell, IXLCell xlCell)
    {
        if (cell.Colspan is null or < 2 && cell.Rowspan is null or < 2)
        {
            return false;
        }

        var (colStart, rowStart) = (xlCell.Address.ColumnNumber, xlCell.Address.RowNumber);
        var (colEnd, rowEnd) = (colStart, rowStart);
        if (cell.Colspan is not null)
        {
            colEnd = xlCell.Address.ColumnNumber + cell.Colspan.Value - 1;
        }

        if (cell.Rowspan is not null)
        {
            rowEnd = xlCell.Address.RowNumber + cell.Rowspan.Value - 1;
        }

        excelWorksheet
            .Range(rowStart, colStart, rowEnd, colEnd)
            .Merge(false);

        return true;
    }

    private XLCellValue GetValue(CellModel cell)
    {
        return cell.DataType switch
        {
            DataType.DateOnly => string.IsNullOrEmpty(cell.Text)
                ? Blank.Value
                : DateTime.TryParseExact(cell.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? (XLCellValue)date : cell.Text,

            DataType.Number => string.IsNullOrEmpty(cell.Text)
                ? Blank.Value
                : double.TryParse(cell.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
                    ? (XLCellValue)number : cell.Text,

            DataType.Boolean => string.IsNullOrEmpty(cell.Text)
                ? Blank.Value
                : bool.TryParse(cell.Text, out var b)
                    ? (XLCellValue)b : cell.Text,

            _ => string.IsNullOrEmpty(cell.Text) ? Blank.Value : global::Sheetloom.ExcelStyleRenderer.PrepareText(cell.Text)
        };
    }

    // How many chars of this font fit per Excel column-width unit relative to Calibri (the Excel default).
    // Calibri chars are wider on average → other fonts get > 1.0 (more chars fit per unit).
    private static double GetFontCharWidthFactor(string? fontName) => fontName?.ToLowerInvariant() switch
    {
        "times new roman" => 1.18,  // TNR chars narrower than Calibri → more fit per column unit
        "arial" => 1.02,
        "courier new" => 0.72,      // monospace, wider per char
        _ => 1.0                    // Calibri and everything else
    };
}
