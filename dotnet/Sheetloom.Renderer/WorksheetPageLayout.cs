using ClosedXML.Excel;

namespace Sheetloom;

internal static class WorksheetPageLayout
{
    internal static void ApplyPrintSettings(WorksheetModel worksheet, IXLWorksheet excelWorksheet)
    {
        if (worksheet.PrintingArea.Orientation != null)
            excelWorksheet.PageSetup.PageOrientation = ToExcel(worksheet.PrintingArea.Orientation.Value);
        if (worksheet.PrintingArea.PaperSize != null)
            excelWorksheet.PageSetup.PaperSize = ToExcel(worksheet.PrintingArea.PaperSize.Value);
    }

    internal static double GetPageHeight(IXLPageSetup excelWorksheetPageSetup)
    {
        return excelWorksheetPageSetup.PageOrientation != XLPageOrientation.Portrait ? 765 : 1600;
    }

    internal static double ComputeFitToPageScale(
        ExcelFormulaRendererInstance formulaRenderer,
        WorksheetModel worksheet,
        IXLWorksheet excelWorksheet)
    {
        if (string.IsNullOrEmpty(worksheet.PrintingArea.Range))
        {
            return 1.0;
        }

        var renderedRange = formulaRenderer.Render(worksheet.PrintingArea.Range);
        var parts = renderedRange.Split(':');
        var (_, firstCol) = ParseR1C1(parts[0]);
        var (_, lastCol) = ParseR1C1(parts[1]);

        if (firstCol == null || lastCol == null)
        {
            return 1.0;
        }

        double totalWidth = 0;
        for (int column = firstCol.Value; column <= lastCol.Value; column++)
        {
            totalWidth += excelWorksheet.Column(column).Width;
        }

        if (totalWidth <= 0)
        {
            return 1.0;
        }

        const double printableWidthInExcelUnits = 200;
        return Math.Min(1.0, printableWidthInExcelUnits / totalWidth);
    }

    internal static double GetRepeatedRowsHeight(
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

    internal static void SetupScrolledFreezePane(ExcelFormulaRendererInstance formulaRenderer, string repeatedRows,
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

    internal static void ApplyRangesAndPageSettings(ExcelFormulaRendererInstance formulaRenderer, WorksheetModel worksheet,
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
        }

        if (!string.IsNullOrEmpty(worksheet.PrintingArea.RowsToRepeatAtTop))
        {
            var extracted = formulaRenderer.Render(worksheet.PrintingArea.RowsToRepeatAtTop);
            var converted = ConvertR1C1ToA1(extracted);
            excelWorksheet.PageSetup.SetRowsToRepeatAtTop(converted);
        }
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
}
