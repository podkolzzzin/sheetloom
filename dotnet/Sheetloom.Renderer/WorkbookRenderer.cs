using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Sheetloom;

public interface IWorkbookRenderer
{
    byte[] Render(string xml, bool precisionAsDisplayed);
}

/// <summary>Request-local workbook construction and serialization; no request state is retained.</summary>
public sealed class WorkbookRenderer : IWorkbookRenderer
{
    public byte[] Render(string xml, bool precisionAsDisplayed)
    {
        var model = new WorkbookParser().Load(xml);
        var renderer = new ExcelTemplateRenderer(new ExcelWorksheetRenderer());
        using var workbook = renderer.Render(model);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        if (precisionAsDisplayed)
        {
            stream.Position = 0;
            using var document = SpreadsheetDocument.Open(stream, true);
            var part = document.WorkbookPart ?? throw new InvalidDataException("Missing workbook part.");
            part.Workbook.CalculationProperties ??= new CalculationProperties();
            part.Workbook.CalculationProperties.FullPrecision = false;
            part.Workbook.Save(part);
        }
        return stream.ToArray();
    }
}
