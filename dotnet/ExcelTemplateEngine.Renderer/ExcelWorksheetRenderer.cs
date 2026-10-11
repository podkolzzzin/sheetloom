using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ClosedXML.Graphics;
using System.Globalization;

namespace ExcelTemplateEngine;

public class ExcelWorksheetRenderer
{
    public void Render(ExcelFormulaRendererInstance formulaRenderer, WorksheetModel worksheet, IXLWorksheet excelWorksheet)
    {
        SetupPrinting(worksheet, excelWorksheet);
        double pageHeight = GetHeight(excelWorksheet.PageSetup);

        if (worksheet.FitColumnsInPage && worksheet.LastPageRow != null)
        {
            // Two-pass: render into a shadow worksheet first so that FitColumn/AdjustToContents
            // settle all column widths, then compute the real scale before the pagination loop.
            var shadowSheet = excelWorksheet.Workbook.AddWorksheet("__scale_probe__");
            SetupPrinting(worksheet, shadowSheet);
            var shadowFormula = new ExcelFormulaRendererInstance();
            RunRenderLoop(shadowFormula, worksheet, shadowSheet, double.MaxValue);
            double scale = ComputeFitToPageScale(shadowFormula, worksheet, shadowSheet);
            shadowSheet.Delete();
            pageHeight /= scale;
        }

        RunRenderLoop(formulaRenderer, worksheet, excelWorksheet, pageHeight);
        PostSetupWorksheet(formulaRenderer, worksheet, excelWorksheet);
    }

    private void RunRenderLoop(ExcelFormulaRendererInstance formulaRenderer, WorksheetModel worksheet, IXLWorksheet excelWorksheet, double pageHeight)
    {
        double currentPageCommulativeHeight = 0;
        double repeatedRowsHeight = 0;
        bool repeatedRowsHeightCalculated = false;
        int pageNumber = 0;
        int pageRowIndex = 0;
        int excelRowIndex = 1;
        int? freezePaneRowIndex = null;
        var pendingFitRows = new List<(IXLCell Cell, IXLRow Row, CellModel CellModel)>();
        var pendingImages = new List<(IXLPicture Picture, IXLRange Range)>();

        ExcelFormulaRendererInstance.Track(worksheet, excelWorksheet);
        formulaRenderer.Track("pageNumber", ++pageNumber);
        bool renderLastPageRow = true;
        foreach (var row in worksheet.Rows)
        {
            if (row.RenderLastPageRow == true)
                renderLastPageRow = true;
            else if (row.RenderLastPageRow == false)
                renderLastPageRow = false;
            var xlRow = excelWorksheet.Row(excelRowIndex);
            formulaRenderer.Track(row, xlRow);
            if (pageRowIndex == 0)
            {
                formulaRenderer.Track("firstPageRow", xlRow.RowNumber());
            }
            pageRowIndex++;

            if (row.FreezePane && freezePaneRowIndex == null)
            {
                freezePaneRowIndex = excelRowIndex;
            }

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

            int lastColumn = RenderCells(formulaRenderer, excelWorksheet, row, xlRow, pendingFitRows, pendingImages);
            ApplyAutoFilter(row, xlRow, lastColumn);

            currentPageCommulativeHeight += !xlRow.IsHidden ? xlRow.Height : 0;
            if (currentPageCommulativeHeight + xlRow.Height * 2 >= pageHeight)
            {
                if (!HasMergedCells(excelWorksheet.Row(excelRowIndex + 1)))
                {
                    formulaRenderer.Track("lastPageRow", xlRow.RowNumber());
                    if (worksheet.LastPageRow != null && renderLastPageRow)
                    {
                        excelRowIndex++;
                        RenderRow(formulaRenderer, excelWorksheet, worksheet.LastPageRow,
                            excelWorksheet.Row(excelRowIndex), pendingFitRows, pendingImages);
                        excelWorksheet.PageSetup.AddHorizontalPageBreak(xlRow.RowNumber() + 1);
                    }

                    currentPageCommulativeHeight = GetRepeatedRowsHeight(
                        ref repeatedRowsHeight, ref repeatedRowsHeightCalculated,
                        formulaRenderer, excelWorksheet, worksheet.PrintingArea);
                    pageRowIndex = 0;
                    formulaRenderer.Track("pageNumber", ++pageNumber);
                }
            }

            excelRowIndex++;
        }

        foreach (var (pendingCell, pendingRow, pendingCellModel) in pendingFitRows)
            ApplyFitRow(pendingCell, pendingRow, pendingCellModel);
        foreach (var (picture, range) in pendingImages)
            PositionImage(picture, range);

        if (freezePaneRowIndex.HasValue)
        {
            if (!worksheet.FreezePaneFromStart &&
                !string.IsNullOrWhiteSpace(worksheet.PrintingArea.RowsToRepeatAtTop))
                SetupScrolledFreezePane(formulaRenderer, worksheet.PrintingArea.RowsToRepeatAtTop, excelWorksheet);
            else
                excelWorksheet.SheetView.Freeze(freezePaneRowIndex.Value, 0);
        }
    }

    private static void SetupScrolledFreezePane(ExcelFormulaRendererInstance formulaRenderer, string repeatedRows,
        IXLWorksheet excelWorksheet)
    {
        var renderedRange = formulaRenderer.Render(repeatedRows);
        var parts = renderedRange.Split(':');
        if (parts.Length != 2)
            throw new InvalidOperationException($"RowsToRepeatAtTop must be a row range, but was '{renderedRange}'.");

        var (firstRow, firstColumn) = ParseR1C1(parts[0]);
        var (lastRow, lastColumn) = ParseR1C1(parts[1]);
        if (firstColumn != null || lastColumn != null || firstRow > lastRow)
            throw new InvalidOperationException($"RowsToRepeatAtTop must be an ascending row range, but was '{renderedRange}'.");

        excelWorksheet.SheetView.Freeze(lastRow - firstRow + 1, 0);
        excelWorksheet.SheetView.TopLeftCellAddress = excelWorksheet.Cell(firstRow, 1).Address;
    }

    private static bool HasMergedCells(IXLRow xlRow)
    {
        return xlRow.CellsUsed(XLCellsUsedOptions.MergedRanges).Any();
    }

    private static double ComputeFitToPageScale(ExcelFormulaRendererInstance formulaRenderer, WorksheetModel worksheet, IXLWorksheet excelWorksheet)
    {
        if (string.IsNullOrEmpty(worksheet.PrintingArea.Range)) return 1.0;

        var renderedRange = formulaRenderer.Render(worksheet.PrintingArea.Range);
        var parts = renderedRange.Split(':');
        var (_, firstCol) = ParseR1C1(parts[0]);
        var (_, lastCol) = ParseR1C1(parts[1]);

        if (firstCol == null || lastCol == null) return 1.0;

        double totalWidth = 0;
        for (int c = firstCol.Value; c <= lastCol.Value; c++) totalWidth += excelWorksheet.Column(c).Width;

        if (totalWidth <= 0) return 1.0;

        const double printableWidthInExcelUnits = 200;
        return Math.Min(1.0, printableWidthInExcelUnits / totalWidth);
    }

    private static double GetRepeatedRowsHeight(
        ref double cachedHeight, ref bool calculated,
        ExcelFormulaRendererInstance formulaRenderer, IXLWorksheet excelWorksheet, PrintingAreaModel printingArea)
    {
        if (calculated)
            return cachedHeight;

        calculated = true;

        if (string.IsNullOrEmpty(printingArea.RowsToRepeatAtTop))
            return 0;

        var rendered = formulaRenderer.Render(printingArea.RowsToRepeatAtTop);
        var parts = rendered.Split(':');
        var startRow = int.Parse(parts[0].Trim().TrimStart('R'));
        var endRow = int.Parse(parts[1].Trim().TrimStart('R'));

        double height = 0;
        for (int row = startRow; row <= endRow; row++)
        {
            var xlRow = excelWorksheet.Row(row);
            if (!xlRow.IsHidden)
                height += xlRow.Height;
        }

        cachedHeight = height;
        return height;
    }

    private static void SetupPrinting(WorksheetModel worksheet, IXLWorksheet excelWorksheet)
    {
        if (worksheet.PrintingArea.Orientation != null)
            excelWorksheet.PageSetup.PageOrientation = ToExcel(worksheet.PrintingArea.Orientation.Value);
        if (worksheet.PrintingArea.PaperSize != null)
            excelWorksheet.PageSetup.PaperSize = ToExcel(worksheet.PrintingArea.PaperSize.Value);
    }

    private static void PostSetupWorksheet(ExcelFormulaRendererInstance formulaRenderer, WorksheetModel worksheet,
        IXLWorksheet excelWorksheet)
    {
        if (!string.IsNullOrEmpty(worksheet.PrintingArea.Range))
        {
            var extracted = formulaRenderer.Render(worksheet.PrintingArea.Range);
            var converted = ConvertR1C1ToA1(extracted);
            excelWorksheet.PageSetup.PrintAreas.Add(converted);
            excelWorksheet.SheetView.View = XLSheetViewOptions.PageBreakPreview;
        }

        foreach (var namedRange in worksheet.NamedRanges)
        {
            var extracted = formulaRenderer.Render(namedRange.Value);
            var converted = ConvertR1C1ToA1(extracted);
            excelWorksheet.DefinedNames.Add(namedRange.Name, converted);
        }

        if (worksheet.FitColumnsInPage)
        {
            excelWorksheet.PageSetup.FitToPages(1, 0);
            //double scale = ComputeFitToPageScale(formulaRenderer, worksheet, excelWorksheet);
            //excelWorksheet.PageSetup.Scale = Math.Max(10, (int)(scale * 100));
        }

        if (!string.IsNullOrEmpty(worksheet.PrintingArea.RowsToRepeatAtTop))
        {
            var extracted = formulaRenderer.Render(worksheet.PrintingArea.RowsToRepeatAtTop);
            var converted = ConvertR1C1ToA1(extracted);
            excelWorksheet.PageSetup.SetRowsToRepeatAtTop(converted);
        }
    }

    private void RenderRow(ExcelFormulaRendererInstance formulaRenderer, IXLWorksheet excelWorksheet, RowModel row, IXLRow xlRow, List<(IXLCell Cell, IXLRow Row, CellModel CellModel)> pendingFitRows, List<(IXLPicture Picture, IXLRange Range)> pendingImages)
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

        int lastColumn = RenderCells(formulaRenderer, excelWorksheet, row, xlRow, pendingFitRows, pendingImages);
        ApplyAutoFilter(row, xlRow, lastColumn);
    }

    private static void ApplyAutoFilter(RowModel row, IXLRow xlRow, int lastColumn)
    {
        if (!row.AutoFilter) return;

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

    private static double GetHeight(IXLPageSetup excelWorksheetPageSetup)
    {
        return excelWorksheetPageSetup.PageOrientation != XLPageOrientation.Portrait ? 765 : 1600;
    }

    private static XLPageOrientation ToExcel(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Landscape => XLPageOrientation.Landscape,
            Orientation.Portrait => XLPageOrientation.Portrait,
            _ => throw new NotImplementedException(orientation.ToString())
        };
    }

    private static XLPaperSize ToExcel(PaperSize paper)
    {
        return paper switch
        {
            PaperSize.A3 => XLPaperSize.A3Paper,
            PaperSize.A4 => XLPaperSize.A4Paper,
            _ => throw new NotImplementedException(paper.ToString())
        };
    }

    private static string ConvertR1C1ToA1(string r1c1)
    {
        string[] parts = r1c1.Split(':');
        var (startRow, startCol) = ParseR1C1(parts[0]);
        var (endRow, endCol) = ParseR1C1(parts[1]);

        var startCell = startCol == null
            ? $"${startRow}"
            : $"${XLHelper.GetColumnLetterFromNumber(startCol.Value)}${startRow}";

        var endCell = endCol == null
            ? $"${endRow}"
            : $"${XLHelper.GetColumnLetterFromNumber(endCol.Value)}${endRow}";

        return $"{startCell}:{endCell}";
    }

    private static (int row, int? col) ParseR1C1(string r1c1)
    {
        r1c1 = r1c1.Trim();
        if (r1c1.Contains('C'))
        {
            // Example: "R3C2" -> row=3, col=2
            var rPart = r1c1[1..r1c1.IndexOf('C')];
            var cPart = r1c1[(r1c1.IndexOf('C') + 1)..];

            return (int.Parse(rPart), int.Parse(cPart));
        }
        else
        {
            // Example: "R3" -> row=3, col=null
            var rPart = r1c1[1..];
            return (int.Parse(rPart), null);
        }
    }

    private int RenderCells(ExcelFormulaRendererInstance formulaRenderer, IXLWorksheet excelWorksheet, RowModel row, IXLRow xlRow, List<(IXLCell Cell, IXLRow Row, CellModel CellModel)> pendingFitRows, List<(IXLPicture Picture, IXLRange Range)> pendingImages)
    {
        int lastColumn = 0;
        for (int cellIndex = 0, modelIndex = 0; modelIndex < row.Cells.Length; cellIndex++)
        {
            var xlCell = xlRow.Cell(cellIndex + 1);
            if (xlCell.IsMerged()) continue;

            var cell = row.Cells[modelIndex];
            if (cell.Colspan is > 1 || cell.Rowspan is > 1)
            {
                var candidate = excelWorksheet.Range(xlCell.Address.RowNumber, xlCell.Address.ColumnNumber,
                    xlCell.Address.RowNumber + Math.Max(cell.Rowspan ?? 1, 1) - 1,
                    xlCell.Address.ColumnNumber + Math.Max(cell.Colspan ?? 1, 1) - 1);
                if (excelWorksheet.MergedRanges.Any(range => range.Intersects(candidate))) continue;
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
                ApplyImage(excelWorksheet, xlCell, cell, pendingImages);
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
            if (literalText is not null) xlCell.GetRichText().AddText(literalText);

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

    // How many chars of this font fit per Excel column-width unit relative to Calibri (the Excel default).
    // Calibri chars are wider on average → other fonts get > 1.0 (more chars fit per unit).
    private static double GetFontCharWidthFactor(string? fontName) => fontName?.ToLowerInvariant() switch
    {
        "times new roman" => 1.18,  // TNR chars narrower than Calibri → more fit per column unit
        "arial" => 1.02,
        "courier new" => 0.72,      // monospace, wider per char
        _ => 1.0                    // Calibri and everything else
    };

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

    private static void ApplyImage(IXLWorksheet excelWorksheet, IXLCell xlCell, CellModel cell, List<(IXLPicture Picture, IXLRange Range)> pendingImages)
    {
        var bytes = TryDecodeImage(cell.ImageSource);
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        using var stream = new MemoryStream(bytes);
        var picture = excelWorksheet.AddPicture(stream);

        // Get the cell or merged range
        var targetRange = xlCell.IsMerged() ? xlCell.MergedRange() : xlCell.AsRange();

        pendingImages.Add((picture, targetRange));
    }

    internal static void PositionImage(IXLPicture picture, IXLRange range)
    {
        var last = range.LastCell();
        int lastRow = last.Address.RowNumber, lastColumn = last.Address.ColumnNumber;
        int endRow = Math.Min(lastRow + 1, XLHelper.MaxRowNumber);
        int endColumn = Math.Min(lastColumn + 1, XLHelper.MaxColumnNumber);
        int offsetX = 0, offsetY = 0;
        // The usual marker is the next cell's top-left corner. At the sheet edge use a
        // positive offset within the last legal marker, never an out-of-bounds cell.
        if (lastColumn == XLHelper.MaxColumnNumber)
        {
            var engine = new DefaultGraphicEngine(Environment.GetEnvironmentVariable("RENDERER_FALLBACK_FONT") ?? "DejaVu Sans");
            int digitWidth = (int)Math.Round(engine.GetMaxDigitWidth(range.Worksheet.Workbook.Style.Font, 96), MidpointRounding.AwayFromZero);
            int padding = 2 * (int)Math.Ceiling(digitWidth / 4.0) + 1;
            double width = last.WorksheetColumn().Width;
            offsetX = (int)Math.Round(width < 1 ? width * (digitWidth + padding) : width * digitWidth + padding, MidpointRounding.AwayFromZero);
        }
        if (lastRow == XLHelper.MaxRowNumber)
            offsetY = (int)Math.Round(last.WorksheetRow().Height * 96 / 72, MidpointRounding.AwayFromZero);
        picture.MoveTo(range.FirstCell(), 0, 0, range.Worksheet.Cell(endRow, endColumn), offsetX, offsetY);
        picture.Placement = XLPicturePlacement.MoveAndSize;
    }

    private static byte[]? TryDecodeImage(string? imageSource)
    {
        if (string.IsNullOrWhiteSpace(imageSource))
        {
            return null;
        }

        const string base64Marker = "base64,";
        var base64Index = imageSource.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
        if (base64Index < 0)
        {
            return null;
        }

        var base64 = imageSource[(base64Index + base64Marker.Length)..];
        return Convert.FromBase64String(base64);
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
            if (value.IsText && value.GetText().StartsWith("'", StringComparison.Ordinal)) return value.GetText();
            xlCell.SetValue(value);
        }
        return null;
    }

    private static bool ApplyMerge(IXLWorksheet excelWorksheet, CellModel cell, IXLCell xlCell)
    {
        if (cell.Colspan is null or < 2 && cell.Rowspan is null or < 2) return false;

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

            _ => string.IsNullOrEmpty(cell.Text) ? Blank.Value : global::ExcelTemplateEngine.ExcelStyleRenderer.PrepareText(cell.Text)
        };
    }
}
