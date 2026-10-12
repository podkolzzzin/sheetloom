using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class PaginationTests
{
    static PaginationTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

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
        using var book = Open(Xml(rows, extra: "<LastPageRow><Cell>footer</Cell></LastPageRow>"));
        Assert.DoesNotContain(book.Worksheet(1).CellsUsed(), cell => cell.GetString() == "footer");
    }

    [Fact]
    public void PageTotalsNeverSplitARowspanAndReferToTheWholePage()
    {
        string rows = "<Row style=\"height:400\"><Cell>one</Cell><Cell data-type=\"Number\">1</Cell></Row>" +
            "<Row style=\"height:400\"><Cell rowspan=\"2\">group</Cell><Cell data-type=\"Number\">2</Cell></Row>" +
            "<Row style=\"height:400\"><Cell data-type=\"Number\">3</Cell></Row>" +
            "<Row style=\"height:400\"><Cell>four</Cell><Cell data-type=\"Number\">4</Cell></Row>";
        using var book = Open(Xml(rows, extra: "<PrintingArea orientation=\"Portrait\">R1C1:R5C2</PrintingArea><LastPageRow><Cell>footer</Cell><Cell><FormulaR1C1>SUM(R{firstPageRow}C2:R{lastPageRow}C2)</FormulaR1C1></Cell></LastPageRow>"));
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
        using var book = Open(Xml(rows, extra: "<PrintingArea orientation=\"Portrait\">R1C1:R9C2</PrintingArea><LastPageRow><Cell><FormulaR1C1>SUM(R{firstPageRow}C1:R{lastPageRow}C1)</FormulaR1C1></Cell><Cell><FormulaR1C1>{pageNumber}</FormulaR1C1></Cell></LastPageRow>"));
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
        using var repeated = Open(Xml(rows, extra: footer + "<PrintingArea>R1C1:R16C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        using var plain = Open(Xml(rows, extra: footer + "<PrintingArea orientation=\"Portrait\">R1C1:R16C1</PrintingArea>"));
        Assert.Equal(new[] { 7, 13, 19 }, FooterRows(repeated.Worksheet(1)));
        Assert.Equal(new[] { 7, 14 }, FooterRows(plain.Worksheet(1)));
    }

    [Fact]
    public void HiddenRepeatedHeaderDoesNotReservePrintedHeight()
    {
        string body = string.Concat(Enumerable.Repeat("<Row style=\"height:200\"><Cell>body</Cell></Row>", 15));
        const string footer = "<LastPageRow><Cell>footer</Cell></LastPageRow>";
        using var repeated = Open(Xml("<Row hide=\"true\" style=\"height:300\"><Cell>heading</Cell></Row>" + body, extra: footer + "<PrintingArea>R1C1:R16C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>"));
        using var plain = Open(Xml("<Row hide=\"true\" style=\"height:300\"><Cell>heading</Cell></Row>" + body, extra: footer + "<PrintingArea orientation=\"Portrait\">R1C1:R16C1</PrintingArea>"));
        Assert.Equal(FooterRows(plain.Worksheet(1)), FooterRows(repeated.Worksheet(1)));
    }

    [Fact]
    public void FitToPageUsesFinalWidthsInsteadOfWidthsSeenAtFirstRow()
    {
        // Place the width declaration only at the end; pagination must see it during the probe pass.
        string late = Xml(string.Concat(Enumerable.Range(1, 12).Select(i => $"<Row style=\"height:200\"><Cell{(i == 12 ? " style=\"width:250\"" : "")}>body</Cell></Row>")), "fit-columns-to-page=\"true\"", "<PrintingArea>R1C1:R12C1</PrintingArea><LastPageRow><Cell>footer</Cell></LastPageRow>");
        string early = late.Replace("<Cell>body</Cell>", "<Cell style=\"width:250\">body</Cell>");
        using var lateBook = Open(late);
        using var earlyBook = Open(early);
        Assert.Equal(FooterRows(earlyBook.Worksheet(1)), FooterRows(lateBook.Worksheet(1)));
        Assert.Equal(new[] { 9 }, FooterRows(lateBook.Worksheet(1)));
        Assert.Single(lateBook.Worksheets);
    }
}
