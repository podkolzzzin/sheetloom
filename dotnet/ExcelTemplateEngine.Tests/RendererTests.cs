using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using ExcelTemplateEngine;

namespace ExcelTemplateEngine.Tests;

public sealed class RendererTests
{
    static RendererTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");
    internal static string Xml(string rows, string attributes = "", string extra = "") =>
        $"<Workbook><Worksheet name=\"Report\" {attributes}>{rows}{extra}</Worksheet></Workbook>";
    internal static XLWorkbook Open(string xml) => new(new MemoryStream(new WorkbookRenderer().Render(xml, false)));

    [Theory]
    [InlineData("Number", "1234.50", XLDataType.Number)]
    [InlineData("Boolean", "true", XLDataType.Boolean)]
    [InlineData("DateOnly", "2024-02-29", XLDataType.DateTime)]
    [InlineData("Text", "Привіт &amp; світ", XLDataType.Text)]
    [InlineData("Number", "not a number", XLDataType.Text)]
    [InlineData("DateOnly", "2024-02-30", XLDataType.Text)]
    [InlineData("Boolean", "maybe", XLDataType.Text)]
    [InlineData("unknown", "abc", XLDataType.Text)]
    public void TypesAndFallbackSurviveSerialization(string type, string text, XLDataType expected)
    {
        using var book = Open(Xml($"<Row><Cell data-type=\"{type}\">{text}</Cell></Row>"));
        Assert.Equal(expected, book.Worksheet(1).Cell("A1").DataType);
    }

    [Fact]
    public void ValuesAreCorrectAndCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            using var book = Open(Xml("<Row><Cell data-type=\"Number\">12.75</Cell><Cell data-type=\"Boolean\">true</Cell><Cell data-type=\"DateOnly\">2024-02-29</Cell><Cell>Україна &amp; &lt;br/&gt; мир</Cell></Row>"));
            var sheet = book.Worksheet(1);
            Assert.Equal(12.75, sheet.Cell("A1").GetDouble());
            Assert.True(sheet.Cell("B1").GetBoolean());
            Assert.Equal(new DateTime(2024, 2, 29), sheet.Cell("C1").GetDateTime());
            Assert.Equal("Україна & \n мир", sheet.Cell("D1").GetString());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("Number")][InlineData("Boolean")][InlineData("DateOnly")][InlineData("Text")]
    public void EmptyTypedValuesStayBlank(string type)
    {
        using var book = Open(Xml($"<Row><Cell data-type=\"{type}\"/><Cell>anchor</Cell></Row>"));
        Assert.Equal(XLDataType.Blank, book.Worksheet(1).Cell("A1").DataType);
    }

    [Fact]
    public void ReferencesFormulasAndNamesUseRenderedAddresses()
    {
        using var book = Open(Xml("<Row excel-row-ref=\"start\"><Cell excel-column-ref=\"amount\" excel-named=\"Amount\" data-type=\"Number\">5</Cell></Row><Row><Cell><FormulaR1C1>SUM(R { start }C { amount }:R { start }C { amount })</FormulaR1C1></Cell></Row>", extra: "<NamedRange name=\"Amounts\">R{start}C{amount}:R{start}C{amount}</NamedRange>"));
        var sheet = book.Worksheet(1);
        Assert.Equal("SUM(R1C1)", sheet.Cell("A2").FormulaR1C1);
        Assert.Equal(5, sheet.Cell("A2").GetDouble());
        Assert.Equal("A1", sheet.DefinedName("Amount").Ranges.Single().FirstCell().Address.ToString());
        Assert.Equal("A1", sheet.DefinedName("Amounts").Ranges.Single().FirstCell().Address.ToString());
    }

    [Fact]
    public void MergeOccupancyAndCellNamesArePreserved()
    {
        using var book = Open(Xml("<Row><Cell colspan=\"2\" rowspan=\"2\">merged</Cell><Cell>right</Cell></Row><Row><Cell excel-named=\"Next\">next</Cell></Row>"));
        var sheet = book.Worksheet(1);
        Assert.Equal("A1:B2", sheet.MergedRanges.Single().RangeAddress.ToString());
        Assert.Equal("right", sheet.Cell("C1").GetString());
        Assert.Equal("next", sheet.Cell("C2").GetString());
    }

    [Fact]
    public void AllStyleGroupsAndDimensionsAreSerialized()
    {
        using var book = Open("<Workbook style=\"font-family:DejaVu Sans;font-size:10\"><Worksheet name=\"Report\" style=\"font-size:12\"><Row style=\"font-weight:bold;height:25\"><Cell style=\"font-size:16;text-align:center;vertical-align:top;text-direction:vertical;text-wrap:wrap;number-format:0.00;background-color:#ffff00;color:#ff0000;border:1;border-top:2;border-bottom:3;border-left:0;border-right:4;border-color:#0000ff;border-top-color:#00ff00;width:30;height:35\" data-type=\"Number\">12.5</Cell></Row></Worksheet></Workbook>");
        var sheet = book.Worksheet(1); var cell = sheet.Cell("A1"); var style = cell.Style;
        Assert.Equal("DejaVu Sans", style.Font.FontName); Assert.Equal(16, style.Font.FontSize); Assert.True(style.Font.Bold);
        Assert.Equal(XLAlignmentHorizontalValues.Center, style.Alignment.Horizontal); Assert.Equal(XLAlignmentVerticalValues.Top, style.Alignment.Vertical);
        Assert.Equal(90, style.Alignment.TextRotation); Assert.True(style.Alignment.WrapText); Assert.Equal("0.00", style.NumberFormat.Format);
        Assert.Equal("FFFFFF00", style.Fill.BackgroundColor.Color.ToArgb().ToString("X8")); Assert.Equal("FFFF0000", style.Font.FontColor.Color.ToArgb().ToString("X8"));
        Assert.Equal(XLBorderStyleValues.Medium, style.Border.TopBorder); Assert.Equal(XLBorderStyleValues.Thick, style.Border.BottomBorder);
        Assert.Equal(XLBorderStyleValues.None, style.Border.LeftBorder); Assert.Equal(XLBorderStyleValues.DashDot, style.Border.RightBorder);
        Assert.Equal(XLColor.FromHtml("#00ff00"), style.Border.TopBorderColor); Assert.Equal(XLColor.FromHtml("#0000ff"), style.Border.BottomBorderColor);
        Assert.Equal(30, sheet.Column(1).Width, 2); Assert.True(sheet.Row(1).Height >= 35);
    }

    [Theory]
    [InlineData("Portrait", "A4", XLPageOrientation.Portrait, XLPaperSize.A4Paper)]
    [InlineData("Landscape", "A3", XLPageOrientation.Landscape, XLPaperSize.A3Paper)]
    public void PrintingAndFreezeFromStartRemainCompatible(string orientation, string paper, XLPageOrientation expectedOrientation, XLPaperSize expectedPaper)
    {
        using var book = Open(Xml("<Row freeze-pane=\"true\"><Cell>title</Cell></Row><Row hide=\"true\"><Cell>hidden</Cell></Row>", "fit-columns-to-page=\"true\" freeze-pane-from-start=\"true\"", $"<PrintingArea orientation=\"{orientation}\" paper-size=\"{paper}\">R1C1:R2C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        var sheet = book.Worksheet(1);
        Assert.Equal(expectedOrientation, sheet.PageSetup.PageOrientation); Assert.Equal(expectedPaper, sheet.PageSetup.PaperSize);
        Assert.Equal(1, sheet.PageSetup.PagesWide); Assert.Equal(0, sheet.PageSetup.PagesTall); Assert.Equal(1, sheet.SheetView.SplitRow);
        Assert.Equal(1, sheet.PageSetup.FirstRowToRepeatAtTop); Assert.Equal(1, sheet.PageSetup.LastRowToRepeatAtTop);
        Assert.True(sheet.Row(2).IsHidden);
        Assert.Single(sheet.PageSetup.PrintAreas);
        // ClosedXML does not restore this view consistently on load; inspect the persisted contract.
        byte[] bytes = new WorkbookRenderer().Render(Xml("<Row><Cell>x</Cell></Row>", extra:"<PrintingArea>R1C1:R1C1</PrintingArea>"), false);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var worksheetXml = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        Assert.Contains("pageBreakPreview", XDocument.Load(worksheetXml).ToString());
    }

    [Fact]
    public void ScrolledFreezeUsesOnlyRepeatedHeaderRows()
    {
        using var book = Open(Xml("<Row><Cell>title</Cell></Row><Row><Cell>head1</Cell></Row><Row freeze-pane=\"true\"><Cell>head2</Cell></Row>", extra:"<PrintingArea>R1C1:R3C1<RowsToRepeatAtTop>R2:R3</RowsToRepeatAtTop></PrintingArea>"));
        Assert.Equal(2, book.Worksheet(1).SheetView.SplitRow);
        Assert.Equal("A2", book.Worksheet(1).SheetView.TopLeftCellAddress.ToString());
    }

    [Fact]
    public void AutoFilterManualBreaksAndPaginationSurviveSerialization()
    {
        string rows = "<Row auto-filter=\"true\" style=\"break-after:always;height:400\"><Cell style=\"break-after:always\">header</Cell><Cell>other</Cell></Row>" + string.Concat(Enumerable.Repeat("<Row style=\"height:400\"><Cell>body</Cell></Row>", 5));
        using var book = Open(Xml(rows, extra:"<LastPageRow><Cell>footer</Cell></LastPageRow>"));
        var sheet = book.Worksheet(1);
        Assert.True(sheet.AutoFilter.IsEnabled); Assert.Contains(1, sheet.PageSetup.RowBreaks); Assert.Contains(1, sheet.PageSetup.ColumnBreaks);
        Assert.Contains(sheet.CellsUsed(), cell => cell.GetString() == "footer");
    }

    [Fact]
    public void FitToPagePaginationRemovesProbeAndKeepsFooter()
    {
        string rows = string.Concat(Enumerable.Repeat("<Row style=\"height:500\"><Cell style=\"width:250\">body</Cell></Row>", 8));
        using var book = Open(Xml(rows, "fit-columns-to-page=\"true\"", "<PrintingArea>R1C1:R8C1</PrintingArea><LastPageRow><Cell>footer</Cell></LastPageRow>"));
        Assert.Single(book.Worksheets);
        Assert.Equal("Report", book.Worksheet(1).Name);
        Assert.Contains(book.Worksheet(1).CellsUsed(), cell => cell.GetString() == "footer");
    }

    [Fact]
    public void LastPageRowCanBeDisabledInASection()
    {
        string rows = "<Row render-last-page-row=\"false\" style=\"height:500\"><Cell>body</Cell></Row>" +
            string.Concat(Enumerable.Repeat("<Row style=\"height:500\"><Cell>body</Cell></Row>", 5));
        using var book = Open(Xml(rows, extra:"<LastPageRow><Cell>footer</Cell></LastPageRow>"));
        Assert.DoesNotContain(book.Worksheet(1).CellsUsed(), cell => cell.GetString() == "footer");
    }

    [Fact]
    public void LegacyReferenceScopeSpansWorksheetsButNotRequests()
    {
        using var book = Open("<Workbook><Worksheet name=\"First\"><Row><Cell>x</Cell><Cell excel-column-ref=\"legacyColumn\">y</Cell></Row></Worksheet><Worksheet name=\"Second\"><Row><Cell><FormulaR1C1>RC{legacyColumn}</FormulaR1C1></Cell><Cell data-type=\"Number\">8</Cell></Row></Worksheet></Workbook>");
        Assert.Equal("RC2", book.Worksheet(2).Cell("A1").FormulaR1C1);
        Assert.Equal(8, book.Worksheet(2).Cell("A1").GetDouble());
    }

    [Fact]
    public void FitColumnAndWrappedRowIncreaseDimensions()
    {
        using var book = Open(Xml("<Row><Cell fit-column=\"true\">A fairly long column heading</Cell></Row><Row><Cell style=\"width:10\" wrap-text=\"true\">This long line must wrap across many lines in a narrow column.</Cell></Row>"));
        Assert.True(book.Worksheet(1).Row(2).Height > 30);
    }

    [Fact]
    public void EmbeddedImageIsStoredAsMediaAndDrawing()
    {
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
        var bytes = new WorkbookRenderer().Render(Xml($"<Row><Cell colspan=\"2\" rowspan=\"2\" image-src=\"data:image/png;base64,{png}\"/></Row>"), false);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("xl/media/"));
        using var book = new XLWorkbook(new MemoryStream(bytes)); Assert.Single(book.Worksheet(1).Pictures);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void PrecisionOptionChangesSerializedCalculationSettingOnly(bool precision)
    {
        var bytes = new WorkbookRenderer().Render(Xml("<Row><Cell data-type=\"Number\" style=\"number-format:0.00\">1.2345</Cell></Row>"), precision);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var xml = archive.GetEntry("xl/workbook.xml")!.Open();
        var doc = XDocument.Load(xml); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string? fullPrecision = doc.Root!.Element(ns + "calcPr")?.Attribute("fullPrecision")?.Value;
        if (precision) Assert.Contains(fullPrecision, new[] { "0", "false" });
        else Assert.DoesNotContain(fullPrecision, new[] { "0", "false" });
        using var book = new XLWorkbook(new MemoryStream(bytes)); Assert.Equal(1.2345, book.Worksheet(1).Cell(1, 1).GetDouble());
    }

    [Theory]
    [InlineData("<Workbook>")]
    [InlineData("<NotAWorkbook/>")]
    [InlineData("<!DOCTYPE Workbook [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><Workbook><Worksheet name='x'><Row><Cell>&xxe;</Cell></Row></Worksheet></Workbook>")]
    [InlineData("<!DOCTYPE Workbook [<!ENTITY a 'test'>]><Workbook/>")]
    public void MalformedXmlAndDtdAreRejected(string xml) => Assert.ThrowsAny<Exception>(() => new WorkbookRenderer().Render(xml, false));

    [Fact]
    public async Task ConcurrentWorkbooksDoNotShareReferencesOrValues()
    {
        var renderer = new WorkbookRenderer();
        await Task.WhenAll(Enumerable.Range(1, 12).Select(number => Task.Run(() =>
        {
            using var book = new XLWorkbook(new MemoryStream(renderer.Render(Xml($"<Row><Cell excel-column-ref=\"source\" data-type=\"Number\">{number}</Cell><Cell><FormulaR1C1>RC{{source}}*2</FormulaR1C1></Cell></Row>"), false)));
            Assert.Equal(number * 2, book.Worksheet(1).Cell("B1").GetDouble());
        })));
    }
}
