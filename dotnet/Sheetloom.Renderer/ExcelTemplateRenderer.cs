using ClosedXML.Excel;

namespace Sheetloom;

public class ExcelTemplateRenderer(ExcelWorksheetRenderer worksheetRenderer)
{
    public IXLWorkbook Render(WorkbookModel workbookModel)
    {
        var xlWorkbook = new XLWorkbook();

        try
        {
            var formulas = ExcelFormulaRenderer.Create();
            ExcelStyleRenderer.Render(workbookModel.Style, xlWorkbook.Style);
            foreach (var worksheet in workbookModel.Worksheets)
            {
                var excelWorksheet = xlWorkbook.AddWorksheet(worksheet.Name);
                ExcelStyleRenderer.Render(worksheet.Style, excelWorksheet.Style);
                worksheetRenderer.Render(formulas, worksheet, excelWorksheet);
            }

            return xlWorkbook;
        }
        catch
        {
            xlWorkbook.Dispose();
            throw;
        }
    }
}
