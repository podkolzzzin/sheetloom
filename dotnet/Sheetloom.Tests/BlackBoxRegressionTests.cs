using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace Sheetloom.Tests;

public sealed class BlackBoxRegressionTests
{
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
    static BlackBoxRegressionTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Fact]
    public void SpanningCellFindsAWholeFreeRectangleAfterAnEarlierRowspan()
    {
        const string rows = "<Row><Cell>first</Cell><Cell rowspan=\"2\">vertical</Cell></Row><Row><Cell colspan=\"2\" excel-column-ref=\"horizontal\" excel-named=\"Placed\">horizontal</Cell><Cell>last</Cell></Row><Row><Cell><FormulaR1C1>R2C{horizontal}</FormulaR1C1></Cell></Row>";
        using var book = RendererTests.Open(RendererTests.Xml(rows));
        var sheet = book.Worksheet(1); var ranges = sheet.MergedRanges.ToArray();
        for (int i = 0; i < ranges.Length; i++)
            for (int j = i + 1; j < ranges.Length; j++) Assert.False(ranges[i].Intersects(ranges[j]));
        Assert.Equal("vertical", sheet.Cell("B1").GetString());
        Assert.Equal("horizontal", sheet.Cell("C2").GetString());
        Assert.Equal("last", sheet.Cell("E2").GetString());
        Assert.Equal("R2C3", sheet.Cell("A3").FormulaR1C1);
        Assert.Equal("C2", sheet.DefinedName("Placed").Ranges.Single().FirstCell().Address.ToString());
    }

    [Theory]
    [InlineData("Workbook")][InlineData("Worksheet")][InlineData("Row")]
    public void InheritedWrapFitsTheSameAsEffectiveCellWrap(string scope)
    {
        const string text = "The quick brown fox jumps over the lazy dog.";
        string Xml(string wrapScope, bool overrideNowrap = false) =>
            $"<Workbook style=\"font-size:14;{(wrapScope == "Workbook" ? "text-wrap:wrap" : "")}\"><Worksheet name=\"Report\" style=\"{(wrapScope == "Worksheet" ? "text-wrap:wrap" : "")}\"><Row style=\"{(wrapScope == "Row" ? "text-wrap:wrap" : "")}\"><Cell style=\"width:10;{(overrideNowrap ? "text-wrap:nowrap" : wrapScope == "Cell" ? "text-wrap:wrap" : "")}\">{text}</Cell></Row></Worksheet></Workbook>";
        using var inherited = RendererTests.Open(Xml(scope));
        using var explicitWrap = RendererTests.Open(Xml("Cell"));
        using var overridden = RendererTests.Open(Xml(scope, true));
        Assert.Equal(explicitWrap.Worksheet(1).Row(1).Height, inherited.Worksheet(1).Row(1).Height, 3);
        Assert.True(inherited.Worksheet(1).Row(1).Height > 50);
        Assert.False(overridden.Worksheet(1).Cell("A1").Style.Alignment.WrapText);
        Assert.True(overridden.Worksheet(1).Row(1).Height < inherited.Worksheet(1).Row(1).Height);
    }

    [Theory]
    [InlineData(1, 1)][InlineData(1, 2)][InlineData(2, 1)][InlineData(2, 2)]
    public void ImageAnchorCoversTheWholeCellOrMergedRectangle(int columns, int rows)
    {
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml($"<Row><Cell colspan=\"{columns}\" rowspan=\"{rows}\" style=\"width:20;height:50\" image-src=\"data:image/png;base64,{Png}\"/></Row>"), false);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var stream = archive.GetEntry("xl/drawings/drawing1.xml")!.Open();
        var anchor = XDocument.Load(stream).Descendants(Drawing + "twoCellAnchor").Single();
        var from = anchor.Element(Drawing + "from")!; var to = anchor.Element(Drawing + "to")!;
        Assert.Equal("0", from.Element(Drawing + "col")!.Value); Assert.Equal("0", from.Element(Drawing + "row")!.Value);
        Assert.Equal(columns.ToString(), to.Element(Drawing + "col")!.Value);
        Assert.Equal(rows.ToString(), to.Element(Drawing + "row")!.Value);
    }

    [Theory]
    [InlineData(" ")][InlineData("   ")][InlineData("\t")][InlineData("\n")][InlineData(" \t\n ")]
    public void NonemptyWhitespaceTextSurvivesParsingAndSerialization(string text)
    {
        string xml = RendererTests.Xml($"<Row><Cell data-type=\"Text\">{text}</Cell><Cell>marker</Cell></Row>");
        var model = new global::Sheetloom.WorkbookParser().Load(xml);
        Assert.Equal(text, model.Worksheets[0].Rows[0].Cells[0].Text);
        using var book = RendererTests.Open(xml);
        Assert.Equal(XLDataType.Text, book.Worksheet(1).Cell("A1").DataType);
        Assert.Equal(text, book.Worksheet(1).Cell("A1").GetString());
        Assert.Equal("marker", book.Worksheet(1).Cell("B1").GetString());
    }

    [Theory]
    [InlineData("'s-Hertogenbosch")][InlineData("'t Hooft")][InlineData("'=1+2")][InlineData("''double")][InlineData("'")]
    public void LeadingApostropheIsTextContentRatherThanQuotePrefixMetadata(string text)
    {
        using var book = RendererTests.Open(RendererTests.Xml($"<Row><Cell data-type=\"Text\">{text}</Cell></Row>"));
        var cell = book.Worksheet(1).Cell("A1");
        Assert.Equal(text, cell.GetString());
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.False(cell.HasFormula);
        Assert.False(cell.Style.IncludeQuotePrefix);
    }
    [Fact]
    public void ApostropheTextUsesEffectiveFontAndPreservesTheMaximumCellTextLength()
    {
        string text = "'" + new string('x', 32766);
        using var book = RendererTests.Open($"<Workbook style=\"font-family:DejaVu Sans\"><Worksheet name=\"Report\" style=\"font-size:18\"><Row style=\"font-weight:bold\"><Cell style=\"color:#ff0000\">{text}</Cell></Row></Worksheet></Workbook>");
        var cell = book.Worksheet(1).Cell("A1");
        Assert.Equal(text, cell.GetString());
        var run = Assert.Single(cell.GetRichText());
        Assert.Equal("DejaVu Sans", run.FontName);
        Assert.Equal(18, run.FontSize);
        Assert.True(run.Bold);
        Assert.Equal(XLColor.FromHtml("#ff0000"), run.FontColor);
        Assert.False(cell.Style.IncludeQuotePrefix);
    }

    [Theory]
    [InlineData(1, 16384)][InlineData(1048576, 1)][InlineData(1048576, 16384)]
    public void ImageAtSheetBoundaryUsesLegalMarkersWithPositiveOuterEdgeOffsets(int row, int column)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Boundary");
        var cell = sheet.Cell(row, column); cell.WorksheetColumn().Width = 20; cell.WorksheetRow().Height = 50;
        using var png = new MemoryStream(Convert.FromBase64String(Png));
        var picture = sheet.AddPicture(png);
        ExcelWorksheetRenderer.PositionImage(picture, cell.AsRange());
        using var output = new MemoryStream(); workbook.SaveAs(output);
        using var archive = new ZipArchive(new MemoryStream(output.ToArray()));
        using var stream = archive.GetEntry("xl/drawings/drawing1.xml")!.Open();
        var end = XDocument.Load(stream).Descendants(Drawing + "to").Single();
        Assert.Equal(Math.Min(column, 16383).ToString(), end.Element(Drawing + "col")!.Value);
        Assert.Equal(Math.Min(row, 1048575).ToString(), end.Element(Drawing + "row")!.Value);
        if (column == 16384) Assert.True(long.Parse(end.Element(Drawing + "colOff")!.Value) > 0);
        if (row == 1048576) Assert.True(long.Parse(end.Element(Drawing + "rowOff")!.Value) > 0);
    }

}
