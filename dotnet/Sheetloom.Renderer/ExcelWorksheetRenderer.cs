using ClosedXML.Excel;

namespace Sheetloom;

public class ExcelWorksheetRenderer
{
    public void Render(
        ExcelFormulaRendererInstance formulaRenderer,
        WorksheetModel worksheet,
        IXLWorksheet excelWorksheet)
    {
        WorksheetPageLayout.ApplyPrintSettings(worksheet, excelWorksheet);
        double pageHeight = WorksheetPageLayout.GetPageHeight(excelWorksheet.PageSetup);

        if (worksheet.FitColumnsInPage && worksheet.LastPageRow != null)
        {
            // Two-pass: render into a shadow worksheet first so that FitColumn/AdjustToContents
            // settle all column widths, then compute the real scale before the pagination loop.
            var shadowSheet = excelWorksheet.Workbook.AddWorksheet("__scale_probe__");
            WorksheetPageLayout.ApplyPrintSettings(worksheet, shadowSheet);
            var shadowFormula = new ExcelFormulaRendererInstance();
            RunRenderLoop(shadowFormula, worksheet, shadowSheet, double.MaxValue);
            double scale = WorksheetPageLayout.ComputeFitToPageScale(shadowFormula, worksheet, shadowSheet);
            shadowSheet.Delete();
            pageHeight /= scale;
        }

        RunRenderLoop(formulaRenderer, worksheet, excelWorksheet, pageHeight);
        WorksheetPageLayout.ApplyRangesAndPageSettings(formulaRenderer, worksheet, excelWorksheet);
    }

    private void RunRenderLoop(
        ExcelFormulaRendererInstance formulaRenderer,
        WorksheetModel worksheet,
        IXLWorksheet excelWorksheet,
        double pageHeight)
    {
        double currentPageHeight = 0;
        double repeatedRowsHeight = 0;
        bool repeatedRowsHeightCalculated = false;
        int pageNumber = 0;
        int pageRowIndex = 0;
        int excelRowIndex = 1;
        int? freezePaneRowIndex = null;
        var cells = new ExcelCellRenderer();

        ExcelFormulaRendererInstance.Track(worksheet, excelWorksheet);
        formulaRenderer.Track("pageNumber", ++pageNumber);
        bool renderLastPageRow = true;
        foreach (var row in worksheet.Rows)
        {
            if (row.RenderLastPageRow == true)
            {
                renderLastPageRow = true;
            }
            else if (row.RenderLastPageRow == false)
            {
                renderLastPageRow = false;
            }

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

            cells.RenderRow(formulaRenderer, excelWorksheet, row, xlRow);

            currentPageHeight += !xlRow.IsHidden ? xlRow.Height : 0;
            if (currentPageHeight + xlRow.Height * 2 >= pageHeight)
            {
                if (!HasMergedCells(excelWorksheet.Row(excelRowIndex + 1)))
                {
                    formulaRenderer.Track("lastPageRow", xlRow.RowNumber());
                    if (worksheet.LastPageRow != null && renderLastPageRow)
                    {
                        excelRowIndex++;
                        cells.RenderRow(formulaRenderer, excelWorksheet, worksheet.LastPageRow,
                            excelWorksheet.Row(excelRowIndex));
                        excelWorksheet.PageSetup.AddHorizontalPageBreak(xlRow.RowNumber() + 1);
                    }

                    currentPageHeight = WorksheetPageLayout.GetRepeatedRowsHeight(
                        ref repeatedRowsHeight, ref repeatedRowsHeightCalculated,
                        formulaRenderer, excelWorksheet, worksheet.PrintingArea);
                    pageRowIndex = 0;
                    formulaRenderer.Track("pageNumber", ++pageNumber);
                }
            }

            excelRowIndex++;
        }

        // Final column widths and row heights are needed before positioning images.
        cells.CompleteLayout();

        if (freezePaneRowIndex.HasValue)
        {
            if (!worksheet.FreezePaneFromStart &&
                !string.IsNullOrWhiteSpace(worksheet.PrintingArea.RowsToRepeatAtTop))
            {
                WorksheetPageLayout.SetupScrolledFreezePane(
                    formulaRenderer, worksheet.PrintingArea.RowsToRepeatAtTop, excelWorksheet);
            }
            else
            {
                excelWorksheet.SheetView.Freeze(freezePaneRowIndex.Value, 0);
            }
        }
    }

    private static bool HasMergedCells(IXLRow xlRow)
    {
        return xlRow.CellsUsed(XLCellsUsedOptions.MergedRanges).Any();
    }
}
