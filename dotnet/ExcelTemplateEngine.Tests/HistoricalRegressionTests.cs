using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace ExcelTemplateEngine.Tests;

/// <summary>Workbook behavior regression coverage; see docs/test-history-renderer.md.</summary>
public sealed class HistoricalRegressionTests
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
    static HistoricalRegressionTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");
    private static byte[] Render(string xml, bool precision = false) => new WorkbookRenderer().Render(xml, precision);
    private static string Workbook(string rows, string attributes = "", string extra = "") => RendererTests.Xml(rows, attributes, extra);
    private static XDocument Part(byte[] bytes, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var stream = archive.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }
    private static int[] FooterRows(IXLWorksheet sheet) => sheet.Column(1).CellsUsed().Where(c => c.GetString() == "footer").Select(c => c.Address.RowNumber).ToArray();

    [Fact]
    public void IdenticalDefinedNamesInDifferentSheetsStayLocalAndQuoteSheetNames()
    {
        byte[] bytes = Render("<Workbook><Worksheet name=\"First's sheet\"><Row><Cell excel-named=\"Amount\" data-type=\"Number\">12</Cell></Row></Worksheet><Worksheet name=\"Second\"><Row><Cell>x</Cell><Cell excel-named=\"Amount\" data-type=\"Number\">34</Cell></Row></Worksheet></Workbook>");
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Empty(book.DefinedNames);
        Assert.Equal(12, book.Worksheet(1).DefinedName("Amount").Ranges.Single().FirstCell().GetDouble());
        Assert.Equal(34, book.Worksheet(2).DefinedName("Amount").Ranges.Single().FirstCell().GetDouble());
        var names = Part(bytes, "xl/workbook.xml").Descendants(Spreadsheet + "definedName").ToArray();
        Assert.Equal(new[] { "0", "1" }, names.Select(n => n.Attribute("localSheetId")!.Value));
        Assert.Contains("'First''s sheet'!", names[0].Value);
    }

    [Theory]
    [InlineData("R{row}C{amount}")]
    [InlineData("R { row }C { amount }")]
    [InlineData("R\n{row}C\t{amount}")]
    public void FormulaWhitespaceAndSimilarNamesResolveExactReferencedCell(string reference)
    {
        byte[] bytes = Render(Workbook($"<Row excel-row-ref=\"row rows\"><Cell excel-column-ref=\"amount\" data-type=\"Number\">3</Cell><Cell excel-column-ref=\"amountTotal\" data-type=\"Number\">7</Cell></Row><Row><Cell><FormulaR1C1>{reference}+R{{rows}}C{{amountTotal}}</FormulaR1C1></Cell></Row>"));
        var cell = Part(bytes, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "c").Single(c => c.Attribute("r")?.Value == "A2");
        Assert.Equal("$A$1+$B$1", cell.Element(Spreadsheet + "f")!.Value);
        Assert.DoesNotContain("{", cell.Element(Spreadsheet + "f")!.Value);
    }

    [Fact]
    public void PageTotalsNeverSplitARowspanAndReferToTheWholePage()
    {
        string rows = "<Row style=\"height:400\"><Cell>one</Cell><Cell data-type=\"Number\">1</Cell></Row>" +
            "<Row style=\"height:400\"><Cell rowspan=\"2\">group</Cell><Cell data-type=\"Number\">2</Cell></Row>" +
            "<Row style=\"height:400\"><Cell data-type=\"Number\">3</Cell></Row>" +
            "<Row style=\"height:400\"><Cell>four</Cell><Cell data-type=\"Number\">4</Cell></Row>";
        using var book = RendererTests.Open(Workbook(rows, extra:"<PrintingArea orientation=\"Portrait\">R1C1:R5C2</PrintingArea><LastPageRow><Cell>footer</Cell><Cell><FormulaR1C1>SUM(R{firstPageRow}C2:R{lastPageRow}C2)</FormulaR1C1></Cell></LastPageRow>"));
        var sheet = book.Worksheet(1);
        Assert.Equal(new[] { 4 }, FooterRows(sheet));
        Assert.Equal("A2:A3", sheet.MergedRanges.Single().RangeAddress.ToString());
        Assert.Equal("SUM(R1C2:R3C2)", sheet.Cell("B4").FormulaR1C1);
        Assert.Equal(3, sheet.Cell("B3").GetDouble());
        Assert.Equal(4, sheet.Cell("B5").GetDouble());
        Assert.Equal(new[] { 4 }, sheet.PageSetup.RowBreaks);
    }

    [Fact]
    public void SuccessivePageTotalsExcludeEarlierFootersAndUsePhysicalRowNumbers()
    {
        string rows = string.Concat(Enumerable.Range(1, 6).Select(i => $"<Row style=\"height:400\"><Cell data-type=\"Number\">{i}</Cell></Row>"));
        using var book = RendererTests.Open(Workbook(rows, extra:"<PrintingArea orientation=\"Portrait\">R1C1:R9C2</PrintingArea><LastPageRow><Cell><FormulaR1C1>SUM(R{firstPageRow}C1:R{lastPageRow}C1)</FormulaR1C1></Cell><Cell><FormulaR1C1>{pageNumber}</FormulaR1C1></Cell></LastPageRow>"));
        var sheet = book.Worksheet(1);
        Assert.Equal(new[] { 3, 6, 9 }, sheet.PageSetup.RowBreaks);
        Assert.Equal("SUM(R1C1:R2C1)", sheet.Cell("A3").FormulaR1C1);
        Assert.Equal("SUM(R4C1:R5C1)", sheet.Cell("A6").FormulaR1C1);
        Assert.Equal("SUM(R7C1:R8C1)", sheet.Cell("A9").FormulaR1C1);
        Assert.Equal(new[] { "1", "2", "3" }, new[] { "B3", "B6", "B9" }.Select(address => sheet.Cell(address).FormulaR1C1));
    }

    [Fact]
    public void RepeatedHeaderHeightIsReservedOnLaterPages()
    {
        string rows = "<Row style=\"height:300\"><Cell>heading</Cell></Row>" + string.Concat(Enumerable.Repeat("<Row style=\"height:200\"><Cell>body</Cell></Row>", 15));
        const string footer = "<LastPageRow><Cell>footer</Cell></LastPageRow>";
        using var repeated = RendererTests.Open(Workbook(rows, extra: footer + "<PrintingArea>R1C1:R16C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        using var plain = RendererTests.Open(Workbook(rows, extra: footer + "<PrintingArea orientation=\"Portrait\">R1C1:R16C1</PrintingArea>"));
        Assert.Equal(new[] { 7, 13, 19 }, FooterRows(repeated.Worksheet(1)));
        Assert.Equal(new[] { 7, 14 }, FooterRows(plain.Worksheet(1)));
    }

    [Fact]
    public void HiddenRepeatedHeaderDoesNotReservePrintedHeight()
    {
        string body = string.Concat(Enumerable.Repeat("<Row style=\"height:200\"><Cell>body</Cell></Row>", 15));
        const string footer = "<LastPageRow><Cell>footer</Cell></LastPageRow>";
        using var repeated = RendererTests.Open(Workbook("<Row hide=\"true\" style=\"height:300\"><Cell>heading</Cell></Row>" + body, extra: footer + "<PrintingArea>R1C1:R16C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        using var plain = RendererTests.Open(Workbook("<Row hide=\"true\" style=\"height:300\"><Cell>heading</Cell></Row>" + body, extra: footer + "<PrintingArea orientation=\"Portrait\">R1C1:R16C1</PrintingArea>"));
        Assert.Equal(FooterRows(plain.Worksheet(1)), FooterRows(repeated.Worksheet(1)));
    }

    [Fact]
    public void FitToPageUsesFinalWidthsInsteadOfWidthsSeenAtFirstRow()
    {
        // Place the width declaration only at the end; pagination must see it during the probe pass.
        string late = Workbook(string.Concat(Enumerable.Range(1, 12).Select(i => $"<Row style=\"height:200\"><Cell{(i == 12 ? " style=\"width:250\"" : "")}>body</Cell></Row>")), "fit-columns-to-page=\"true\"", "<PrintingArea>R1C1:R12C1</PrintingArea><LastPageRow><Cell>footer</Cell></LastPageRow>");
        string early = late.Replace("<Cell>body</Cell>", "<Cell style=\"width:250\">body</Cell>");
        using var lateBook = RendererTests.Open(late); using var earlyBook = RendererTests.Open(early);
        Assert.Equal(FooterRows(earlyBook.Worksheet(1)), FooterRows(lateBook.Worksheet(1)));
        Assert.Equal(new[] { 9 }, FooterRows(lateBook.Worksheet(1)));
        Assert.Single(lateBook.Worksheets);
    }

    [Fact]
    public void DeferredFitUsesFinalColumnWidthInsteadOfFirstRowWidth()
    {
        string longText = new('x', 70);
        using var late = RendererTests.Open(Workbook($"<Row><Cell wrap-text=\"true\">{longText}</Cell></Row><Row><Cell style=\"width:40\">width setter</Cell></Row>"));
        using var early = RendererTests.Open(Workbook($"<Row><Cell wrap-text=\"true\" style=\"width:40\">{longText}</Cell></Row><Row><Cell>width setter</Cell></Row>"));
        Assert.Equal(early.Worksheet(1).Row(1).Height, late.Worksheet(1).Row(1).Height, 3);
        Assert.InRange(late.Worksheet(1).Row(1).Height, 25, 40);
    }

    [Fact]
    public void MergedWrappedCellUsesAllFinalColumnWidthsAndPreservesExplicitMinimumHeight()
    {
        string text = new('x', 60);
        using var merged = RendererTests.Open(Workbook($"<Row><Cell colspan=\"2\" wrap-text=\"true\">{text}</Cell></Row><Row><Cell style=\"width:20\">a</Cell><Cell style=\"width:20\">b</Cell></Row><Row style=\"height:90\"><Cell wrap-text=\"true\">short</Cell></Row>"));
        using var narrow = RendererTests.Open(Workbook($"<Row><Cell wrap-text=\"true\">{text}</Cell></Row><Row><Cell style=\"width:20\">a</Cell></Row>"));
        Assert.True(merged.Worksheet(1).Row(1).Height < narrow.Worksheet(1).Row(1).Height);
        Assert.Equal(90, merged.Worksheet(1).Row(3).Height);
    }

    [Fact]
    public void InheritedFontFamilyAndExplicitNewlinesAffectWrappedHeight()
    {
        string Build(string font) => $"<Workbook style=\"font-family:{font};font-size:11\"><Worksheet name=\"Report\"><Row><Cell wrap-text=\"true\" style=\"width:20\">{new string('x', 100)}</Cell></Row><Row><Cell wrap-text=\"true\" style=\"width:100\">first&lt;br/&gt;second&lt;br/&gt;third</Cell></Row></Worksheet></Workbook>";
        using var narrow = RendererTests.Open(Build("Times New Roman")); using var wide = RendererTests.Open(Build("Courier New"));
        Assert.True(wide.Worksheet(1).Row(1).Height > narrow.Worksheet(1).Row(1).Height);
        Assert.True(narrow.Worksheet(1).Row(2).Height > 40);
        Assert.Equal("first\nsecond\nthird", narrow.Worksheet(1).Cell("A2").GetString());
    }

    [Theory]
    [InlineData(false, "2", "A2")]
    [InlineData(true, "3", "A1")]
    public void FreezeVariantAndPrintPreviewAreVerifiedInSerializedXml(bool fromStart, string frozenRows, string origin)
    {
        byte[] bytes = Render(Workbook("<Row><Cell>title</Cell></Row><Row excel-row-ref=\"headerStart\"><Cell>header one</Cell></Row><Row excel-row-ref=\"headerEnd\" freeze-pane=\"true\"><Cell>header two</Cell></Row><Row><Cell>body</Cell></Row>", $"freeze-pane-from-start=\"{fromStart.ToString().ToLowerInvariant()}\"", "<PrintingArea>R1C1:R4C1<RowsToRepeatAtTop>R{headerStart}:R{headerEnd}</RowsToRepeatAtTop></PrintingArea>"));
        var view = Part(bytes, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "sheetView").Single();
        Assert.Equal("pageBreakPreview", view.Attribute("view")!.Value);
        Assert.Equal(frozenRows, view.Element(Spreadsheet + "pane")!.Attribute("ySplit")!.Value);
        Assert.Equal(origin, view.Attribute("topLeftCell")?.Value ?? "A1");
        Assert.Equal("frozenSplit", view.Element(Spreadsheet + "pane")!.Attribute("state")!.Value);
    }

    [Fact]
    public void FirstFreezeMarkerWinsAndPrintTitlesAloneDoNotFreeze()
    {
        using var first = RendererTests.Open(Workbook("<Row freeze-pane=\"true\"><Cell>a</Cell></Row><Row freeze-pane=\"true\"><Cell>b</Cell></Row>"));
        using var titlesOnly = RendererTests.Open(Workbook("<Row><Cell>a</Cell></Row><Row><Cell>b</Cell></Row>", extra:"<PrintingArea>R1C1:R2C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        Assert.Equal(1, first.Worksheet(1).SheetView.SplitRow);
        Assert.Equal(0, titlesOnly.Worksheet(1).SheetView.SplitRow);
    }

    [Theory]
    [InlineData("R3:R2")][InlineData("R1C1:R2C1")][InlineData("R2")]
    public void InvalidScrolledFreezeRangesFailInsteadOfSilentlyFreezingWrongRows(string range) =>
        Assert.ThrowsAny<Exception>(() => Render(Workbook("<Row freeze-pane=\"true\"><Cell>header</Cell></Row>", extra:$"<PrintingArea>R1C1:R1C1<RowsToRepeatAtTop>{range}</RowsToRepeatAtTop></PrintingArea>")));

    [Fact]
    public void EmbeddedImageBytesAndMergedAnchorSurviveSerialization()
    {
        byte[] bytes = Render(Workbook($"<Row><Cell colspan=\"3\" rowspan=\"2\" image-src=\"data:image/png;base64,{Png}\">ignored text<FormulaR1C1>1+1</FormulaR1C1></Cell></Row>"));
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var source = archive.Entries.Single(e => e.FullName.StartsWith("xl/media/", StringComparison.Ordinal)).Open();
        using var buffer = new MemoryStream(); source.CopyTo(buffer);
        Assert.Equal(Convert.FromBase64String(Png), buffer.ToArray());
        var anchor = Part(bytes, "xl/drawings/drawing1.xml").Descendants(Drawing + "twoCellAnchor").Single();
        Assert.Equal("0", anchor.Element(Drawing + "from")!.Element(Drawing + "col")!.Value);
        Assert.Equal("0", anchor.Element(Drawing + "from")!.Element(Drawing + "row")!.Value);
        Assert.Equal("3", anchor.Element(Drawing + "to")!.Element(Drawing + "col")!.Value);
        Assert.Equal("2", anchor.Element(Drawing + "to")!.Element(Drawing + "row")!.Value);
        Assert.Empty(Part(bytes, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "f"));
    }

    [Fact]
    public void PrecisionOptionDoesNotDropFormulasMergesStylesOrOtherCalculationAttributes()
    {
        string xml = Workbook("<Row><Cell data-type=\"Number\" style=\"number-format:0.00\">1.2345</Cell><Cell colspan=\"2\">merged</Cell></Row><Row><Cell><FormulaR1C1>R1C1*2</FormulaR1C1></Cell></Row>");
        byte[] normal = Render(xml); byte[] rounded = Render(xml, true);
        foreach (string path in new[] { "xl/worksheets/sheet1.xml", "xl/styles.xml", "xl/sharedStrings.xml" })
            Assert.True(XNode.DeepEquals(Part(normal, path), Part(rounded, path)), path);
        var normalWorkbook = Part(normal, "xl/workbook.xml"); var roundedWorkbook = Part(rounded, "xl/workbook.xml");
        var precision = roundedWorkbook.Descendants(Spreadsheet + "calcPr").Single().Attribute("fullPrecision")!;
        Assert.Contains(precision.Value, new[] { "0", "false" });
        precision.Remove(); normalWorkbook.Descendants(Spreadsheet + "calcPr").Single().Attribute("fullPrecision")?.Remove();
        Assert.True(XNode.DeepEquals(normalWorkbook, roundedWorkbook));
    }
}
