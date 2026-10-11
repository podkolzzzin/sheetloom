using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace Sheetloom.Tests;

public sealed class RendererEdgeCaseTests
{
    static RendererEdgeCaseTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Theory]
    [InlineData("<Cell>First</Cell><Cell>Second</Cell>", "A1:B1")]
    [InlineData("<Cell/><Cell/>", "A1:B1")]
    [InlineData("<Cell colspan=\"3\">Group</Cell><Cell>Fourth</Cell>", "A1:D1")]
    public void AutoFilterUsesAnExplicitCellRangeIncludingBlankAndMergedHeaders(string cells, string expected)
    {
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml($"<Row auto-filter=\"true\">{cells}</Row>"), false);
        Assert.Equal(expected, AutoFilterReference(bytes));
    }

    [Fact]
    public void AutoFilterIncludesInheritedRowspanHeadersAfterTheLastEmittedCell()
    {
        const string rows = "<Row><Cell rowspan=\"2\">Left</Cell><Cell>Group</Cell><Cell rowspan=\"2\">Right</Cell></Row><Row auto-filter=\"true\"><Cell>Middle</Cell></Row>";
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml(rows), false);
        Assert.Equal("A2:C2", AutoFilterReference(bytes));
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal("Middle", book.Worksheet(1).Cell("B2").GetString());
        Assert.Equal(2, book.Worksheet(1).MergedRanges.Count);
    }

    [Fact]
    public void AutoFilterIncludesFullyInheritedMergedHeaderWithNoEmittedCells()
    {
        const string rows = "<Row><Cell rowspan=\"2\" colspan=\"3\">Shared header</Cell></Row><Row auto-filter=\"true\"/>";
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml(rows), false);
        Assert.Equal("A2:C2", AutoFilterReference(bytes));
    }

    [Fact]
    public void AutoFilterDoesNotIncludeWiderMergesThatEndedBeforeTheHeaderRow()
    {
        const string rows = "<Row><Cell rowspan=\"2\" colspan=\"5\">Earlier title</Cell></Row><Row/><Row auto-filter=\"true\"><Cell>One</Cell><Cell>Two</Cell></Row>";
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml(rows), false);
        Assert.Equal("A3:B3", AutoFilterReference(bytes));
    }

    [Fact]
    public void FooterAutoFilterAlsoUsesAnExplicitRange()
    {
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml("<Row style=\"height:400\"><Cell>body</Cell></Row>", extra:"<LastPageRow auto-filter=\"true\"><Cell>First</Cell><Cell>Second</Cell></LastPageRow>"), false);
        Assert.Equal("A2:B2", AutoFilterReference(bytes));
    }

    [Fact]
    public void EmptyAutoFilterRowDoesNotEmitAnInvalidWholeRowReference()
    {
        byte[] bytes = new WorkbookRenderer().Render(RendererTests.Xml("<Row auto-filter=\"true\"/><Row><Cell>data</Cell></Row>"), false);
        Assert.Null(AutoFilterReference(bytes));
    }

    private static string? AutoFilterReference(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Load(worksheet).Root!.Element(ns + "autoFilter")?.Attribute("ref")?.Value;
    }

    [Theory]
    [InlineData("th-TH")][InlineData("ar-SA")][InlineData("fa-IR")]
    public void IsoDatesRemainGregorianUnderNonGregorianHostCultures(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var book = RendererTests.Open(RendererTests.Xml("<Row><Cell data-type=\"DateOnly\">2024-02-29</Cell></Row>"));
            Assert.Equal(XLDataType.DateTime, book.Worksheet(1).Cell("A1").DataType);
            Assert.Equal(new DateTime(2024, 2, 29), book.Worksheet(1).Cell("A1").GetDateTime());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("NaN")][InlineData("Infinity")][InlineData("-Infinity")][InlineData("1e9999")]
    public void NonFiniteNumericInputFallsBackToTextLikeOtherInvalidNumbers(string input)
    {
        using var book = RendererTests.Open(RendererTests.Xml($"<Row><Cell data-type=\"Number\">{input}</Cell><Cell data-type=\"Number\">5</Cell></Row>"));
        Assert.Equal(XLDataType.Text, book.Worksheet(1).Cell("A1").DataType);
        Assert.Equal(input, book.Worksheet(1).Cell("A1").GetString());
        Assert.Equal(5, book.Worksheet(1).Cell("B1").GetDouble());
    }

    [Theory]
    [InlineData("=1+1")][InlineData("+123")][InlineData("@SUM(A1)")][InlineData("000123")]
    public void TextLookingLikeFormulaOrIdentifierRemainsLiteral(string text)
    {
        using var book = RendererTests.Open(RendererTests.Xml($"<Row><Cell>{text}</Cell></Row>"));
        var cell = book.Worksheet(1).Cell("A1");
        Assert.Equal(XLDataType.Text, cell.DataType); Assert.Equal(text, cell.GetString()); Assert.False(cell.HasFormula);
    }

    [Theory]
    [InlineData("<SecondRoot/>")][InlineData("trailing garbage")]
    public void TrailingMalformedXmlIsNotSilentlyIgnored(string suffix) =>
        Assert.ThrowsAny<Exception>(() => new WorkbookRenderer().Render(RendererTests.Xml("<Row><Cell>valid prefix</Cell></Row>") + suffix, false));

    [Fact]
    public void ValidEmptyRowsAndWorksheetsDoNotLoseFollowingData()
    {
        using var book = RendererTests.Open("<Workbook><Worksheet name=\"Empty\"/><Worksheet name=\"Data\"><Row/><Row><Cell/><Cell>second column</Cell></Row></Worksheet></Workbook>");
        Assert.Equal(2, book.Worksheets.Count);
        Assert.Equal("second column", book.Worksheet("Data").Cell("B2").GetString());
        Assert.Equal(XLDataType.Blank, book.Worksheet("Data").Cell("A2").DataType);
    }

    [Fact]
    public void CaseInsensitiveDuplicateSheetNamesFailThenSameRendererRecovers()
    {
        var renderer = new WorkbookRenderer();
        Assert.ThrowsAny<Exception>(() => renderer.Render("<Workbook><Worksheet name=\"Report\"/><Worksheet name=\"report\"/></Workbook>", false));
        using var recovered = new XLWorkbook(new MemoryStream(renderer.Render(RendererTests.Xml("<Row><Cell>next request</Cell></Row>"), false)));
        Assert.Equal("next request", recovered.Worksheet(1).Cell("A1").GetString());
    }

    [Theory]
    [InlineData("https://example.invalid/should-not-be-fetched.png")]
    [InlineData("file:///etc/passwd")]
    public void NonBase64ImageSourcesDoNotReadOrFetchExternalResources(string source)
    {
        using var book = RendererTests.Open(RendererTests.Xml($"<Row><Cell image-src=\"{source}\"/><Cell>next</Cell></Row>"));
        Assert.Empty(book.Worksheet(1).Pictures);
        Assert.Equal("next", book.Worksheet(1).Cell("B1").GetString());
    }

    [Theory]
    [InlineData("data:image/png;base64,%%%")]
    [InlineData("data:image/png;base64,aGVsbG8=")]
    public void CorruptImageFailsWithoutPoisoningNextRequest(string source)
    {
        var renderer = new WorkbookRenderer();
        Assert.ThrowsAny<Exception>(() => renderer.Render(RendererTests.Xml($"<Row><Cell image-src=\"{source}\"/></Row>"), false));
        Assert.NotEmpty(renderer.Render(RendererTests.Xml("<Row><Cell>recovered</Cell></Row>"), false));
    }

    [Fact]
    public async Task ConcurrentRequestsWithDifferentReferencePositionsRemainIsolated()
    {
        var renderer = new WorkbookRenderer();
        await Task.WhenAll(Enumerable.Range(1, 20).Select(column => Task.Run(() =>
        {
            string padding = string.Concat(Enumerable.Repeat("<Cell/>", column - 1));
            string xml = RendererTests.Xml($"<Row>{padding}<Cell excel-column-ref=\"amount\" data-type=\"Number\">{column * 3}</Cell><Cell><FormulaR1C1>RC{{amount}}</FormulaR1C1></Cell></Row>");
            using var book = new XLWorkbook(new MemoryStream(renderer.Render(xml, false)));
            Assert.Equal($"RC{column}", book.Worksheet(1).Cell(1, column + 1).FormulaR1C1);
            Assert.Equal(column * 3, book.Worksheet(1).Cell(1, column + 1).GetDouble());
        })));
    }
}
