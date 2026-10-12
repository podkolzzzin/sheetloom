using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace Sheetloom.Tests;

internal static class WorkbookFixture
{
    internal static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    internal const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";

    static WorkbookFixture() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    internal static string Xml(string rows, string attributes = "", string extra = "") =>
        $"<Workbook><Worksheet name=\"Report\" {attributes}>{rows}{extra}</Worksheet></Workbook>";

    internal static byte[] Render(string xml, bool precision = false) => new WorkbookRenderer().Render(xml, precision);

    internal static XLWorkbook Open(string xml) => new(new MemoryStream(Render(xml)));

    internal static XDocument Part(byte[] bytes, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var stream = archive.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }

    internal static int[] FooterRows(IXLWorksheet sheet) => sheet.Column(1).CellsUsed()
        .Where(cell => cell.GetString() == "footer")
        .Select(cell => cell.Address.RowNumber)
        .ToArray();

    internal static string? AutoFilterReference(byte[] bytes) =>
        Part(bytes, "xl/worksheets/sheet1.xml").Root!.Element(Spreadsheet + "autoFilter")?.Attribute("ref")?.Value;
}
