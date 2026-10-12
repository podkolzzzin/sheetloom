using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class ImageTests
{
    static ImageTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Fact]
    public void EmbeddedImageIsStoredAsMediaAndDrawing()
    {
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
        var bytes = new WorkbookRenderer().Render(Xml($"<Row><Cell colspan=\"2\" rowspan=\"2\" image-src=\"data:image/png;base64,{png}\"/></Row>"), false);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("xl/media/"));
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Single(book.Worksheet(1).Pictures);
    }

    [Fact]
    public void EmbeddedImageBytesAndMergedAnchorSurviveSerialization()
    {
        byte[] bytes = Render(Xml($"<Row><Cell colspan=\"3\" rowspan=\"2\" image-src=\"data:image/png;base64,{Png}\">ignored text<FormulaR1C1>1+1</FormulaR1C1></Cell></Row>"));
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var source = archive.Entries.Single(e => e.FullName.StartsWith("xl/media/", StringComparison.Ordinal)).Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        Assert.Equal(Convert.FromBase64String(Png), buffer.ToArray());
        var anchor = Part(bytes, "xl/drawings/drawing1.xml").Descendants(Drawing + "twoCellAnchor").Single();
        Assert.Equal("0", anchor.Element(Drawing + "from")!.Element(Drawing + "col")!.Value);
        Assert.Equal("0", anchor.Element(Drawing + "from")!.Element(Drawing + "row")!.Value);
        Assert.Equal("3", anchor.Element(Drawing + "to")!.Element(Drawing + "col")!.Value);
        Assert.Equal("2", anchor.Element(Drawing + "to")!.Element(Drawing + "row")!.Value);
        Assert.Empty(Part(bytes, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "f"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public void ImageAnchorCoversTheWholeCellOrMergedRectangle(int columns, int rows)
    {
        byte[] bytes = new WorkbookRenderer().Render(Xml($"<Row><Cell colspan=\"{columns}\" rowspan=\"{rows}\" style=\"width:20;height:50\" image-src=\"data:image/png;base64,{Png}\"/></Row>"), false);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var stream = archive.GetEntry("xl/drawings/drawing1.xml")!.Open();
        var anchor = XDocument.Load(stream).Descendants(Drawing + "twoCellAnchor").Single();
        var from = anchor.Element(Drawing + "from")!;
        var to = anchor.Element(Drawing + "to")!;
        Assert.Equal("0", from.Element(Drawing + "col")!.Value);
        Assert.Equal("0", from.Element(Drawing + "row")!.Value);
        Assert.Equal(columns.ToString(), to.Element(Drawing + "col")!.Value);
        Assert.Equal(rows.ToString(), to.Element(Drawing + "row")!.Value);
    }

    [Theory]
    [InlineData(1, 16384)]
    [InlineData(1048576, 1)]
    [InlineData(1048576, 16384)]
    public void ImageAtSheetBoundaryUsesLegalMarkersWithPositiveOuterEdgeOffsets(int row, int column)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Boundary");
        var cell = sheet.Cell(row, column);
        cell.WorksheetColumn().Width = 20;
        cell.WorksheetRow().Height = 50;
        using var png = new MemoryStream(Convert.FromBase64String(Png));
        var picture = sheet.AddPicture(png);
        ExcelImageRenderer.Position(picture, cell.AsRange());
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        using var archive = new ZipArchive(new MemoryStream(output.ToArray()));
        using var stream = archive.GetEntry("xl/drawings/drawing1.xml")!.Open();
        var end = XDocument.Load(stream).Descendants(Drawing + "to").Single();
        Assert.Equal(Math.Min(column, 16383).ToString(), end.Element(Drawing + "col")!.Value);
        Assert.Equal(Math.Min(row, 1048575).ToString(), end.Element(Drawing + "row")!.Value);
        if (column == 16384)
            Assert.True(long.Parse(end.Element(Drawing + "colOff")!.Value) > 0);
        if (row == 1048576)
            Assert.True(long.Parse(end.Element(Drawing + "rowOff")!.Value) > 0);
    }

    [Theory]
    [InlineData("https://example.invalid/should-not-be-fetched.png")]
    [InlineData("file:///etc/passwd")]
    public void NonBase64ImageSourcesDoNotReadOrFetchExternalResources(string source)
    {
        using var book = Open(Xml($"<Row><Cell image-src=\"{source}\"/><Cell>next</Cell></Row>"));
        Assert.Empty(book.Worksheet(1).Pictures);
        Assert.Equal("next", book.Worksheet(1).Cell("B1").GetString());
    }

    [Theory]
    [InlineData("data:image/png;base64,%%%")]
    [InlineData("data:image/png;base64,aGVsbG8=")]
    public void CorruptImageFailsWithoutPoisoningNextRequest(string source)
    {
        var renderer = new WorkbookRenderer();
        Assert.ThrowsAny<Exception>(() => renderer.Render(Xml($"<Row><Cell image-src=\"{source}\"/></Row>"), false));
        Assert.NotEmpty(renderer.Render(Xml("<Row><Cell>recovered</Cell></Row>"), false));
    }
}
