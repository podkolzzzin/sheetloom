using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class PrintLayoutTests
{
    static PrintLayoutTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Theory]
    [InlineData("Portrait", "A4", XLPageOrientation.Portrait, XLPaperSize.A4Paper)]
    [InlineData("Landscape", "A3", XLPageOrientation.Landscape, XLPaperSize.A3Paper)]
    public void PrintingAndFreezeFromStartRemainCompatible(string orientation, string paper, XLPageOrientation expectedOrientation, XLPaperSize expectedPaper)
    {
        using var book = Open(Xml("<Row freeze-pane=\"true\"><Cell>title</Cell></Row><Row hide=\"true\"><Cell>hidden</Cell></Row>", "fit-columns-to-page=\"true\" freeze-pane-from-start=\"true\"", $"<PrintingArea orientation=\"{orientation}\" paper-size=\"{paper}\">R1C1:R2C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        var sheet = book.Worksheet(1);
        Assert.Equal(expectedOrientation, sheet.PageSetup.PageOrientation);
        Assert.Equal(expectedPaper, sheet.PageSetup.PaperSize);
        Assert.Equal(1, sheet.PageSetup.PagesWide);
        Assert.Equal(0, sheet.PageSetup.PagesTall);
        Assert.Equal(1, sheet.SheetView.SplitRow);
        Assert.Equal(1, sheet.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(1, sheet.PageSetup.LastRowToRepeatAtTop);
        Assert.True(sheet.Row(2).IsHidden);
        Assert.Single(sheet.PageSetup.PrintAreas);
        // ClosedXML does not restore this view consistently on load; inspect the persisted contract.
        byte[] bytes = new WorkbookRenderer().Render(Xml("<Row><Cell>x</Cell></Row>", extra: "<PrintingArea>R1C1:R1C1</PrintingArea>"), false);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var worksheetXml = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        Assert.Contains("pageBreakPreview", XDocument.Load(worksheetXml).ToString());
    }

    [Fact]
    public void ScrolledFreezeUsesOnlyRepeatedHeaderRows()
    {
        using var book = Open(Xml("<Row><Cell>title</Cell></Row><Row><Cell>head1</Cell></Row><Row freeze-pane=\"true\"><Cell>head2</Cell></Row>", extra: "<PrintingArea>R1C1:R3C1<RowsToRepeatAtTop>R2:R3</RowsToRepeatAtTop></PrintingArea>"));
        Assert.Equal(2, book.Worksheet(1).SheetView.SplitRow);
        Assert.Equal("A2", book.Worksheet(1).SheetView.TopLeftCellAddress.ToString());
    }

    [Theory]
    [InlineData(false, "2", "A2")]
    [InlineData(true, "3", "A1")]
    public void FreezeVariantAndPrintPreviewAreVerifiedInSerializedXml(bool fromStart, string frozenRows, string origin)
    {
        byte[] bytes = Render(Xml("<Row><Cell>title</Cell></Row><Row excel-row-ref=\"headerStart\"><Cell>header one</Cell></Row><Row excel-row-ref=\"headerEnd\" freeze-pane=\"true\"><Cell>header two</Cell></Row><Row><Cell>body</Cell></Row>", $"freeze-pane-from-start=\"{fromStart.ToString().ToLowerInvariant()}\"", "<PrintingArea>R1C1:R4C1<RowsToRepeatAtTop>R{headerStart}:R{headerEnd}</RowsToRepeatAtTop></PrintingArea>"));
        var view = Part(bytes, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "sheetView").Single();
        Assert.Equal("pageBreakPreview", view.Attribute("view")!.Value);
        Assert.Equal(frozenRows, view.Element(Spreadsheet + "pane")!.Attribute("ySplit")!.Value);
        Assert.Equal(origin, view.Attribute("topLeftCell")?.Value ?? "A1");
        Assert.Equal("frozenSplit", view.Element(Spreadsheet + "pane")!.Attribute("state")!.Value);
    }

    [Fact]
    public void FirstFreezeMarkerWinsAndPrintTitlesAloneDoNotFreeze()
    {
        using var first = Open(Xml("<Row freeze-pane=\"true\"><Cell>a</Cell></Row><Row freeze-pane=\"true\"><Cell>b</Cell></Row>"));
        using var titlesOnly = Open(Xml("<Row><Cell>a</Cell></Row><Row><Cell>b</Cell></Row>", extra: "<PrintingArea>R1C1:R2C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        Assert.Equal(1, first.Worksheet(1).SheetView.SplitRow);
        Assert.Equal(0, titlesOnly.Worksheet(1).SheetView.SplitRow);
    }

    [Theory]
    [InlineData("R3:R2")]
    [InlineData("R1C1:R2C1")]
    [InlineData("R2")]
    public void InvalidScrolledFreezeRangesFailInsteadOfSilentlyFreezingWrongRows(string range) =>
        Assert.ThrowsAny<Exception>(() => Render(Xml("<Row freeze-pane=\"true\"><Cell>header</Cell></Row>", extra: $"<PrintingArea>R1C1:R1C1<RowsToRepeatAtTop>{range}</RowsToRepeatAtTop></PrintingArea>")));
}
