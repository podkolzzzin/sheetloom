using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class AutoFilterTests
{
    static AutoFilterTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Fact]
    public void AutoFilterManualBreaksAndPaginationSurviveSerialization()
    {
        string rows = "<Row auto-filter=\"true\" style=\"break-after:always;height:400\"><Cell style=\"break-after:always\">header</Cell><Cell>other</Cell></Row>" + string.Concat(Enumerable.Repeat("<Row style=\"height:400\"><Cell>body</Cell></Row>", 5));
        using var book = Open(Xml(rows, extra: "<LastPageRow><Cell>footer</Cell></LastPageRow>"));
        var sheet = book.Worksheet(1);
        Assert.True(sheet.AutoFilter.IsEnabled);
        Assert.Contains(1, sheet.PageSetup.RowBreaks);
        Assert.Contains(1, sheet.PageSetup.ColumnBreaks);
        Assert.Contains(sheet.CellsUsed(), cell => cell.GetString() == "footer");
    }

    [Theory]
    [InlineData("<Cell>First</Cell><Cell>Second</Cell>", "A1:B1")]
    [InlineData("<Cell/><Cell/>", "A1:B1")]
    [InlineData("<Cell colspan=\"3\">Group</Cell><Cell>Fourth</Cell>", "A1:D1")]
    public void AutoFilterUsesAnExplicitCellRangeIncludingBlankAndMergedHeaders(string cells, string expected)
    {
        byte[] bytes = new WorkbookRenderer().Render(Xml($"<Row auto-filter=\"true\">{cells}</Row>"), false);
        Assert.Equal(expected, AutoFilterReference(bytes));
    }

    [Fact]
    public void AutoFilterIncludesInheritedRowspanHeadersAfterTheLastEmittedCell()
    {
        const string rows = "<Row><Cell rowspan=\"2\">Left</Cell><Cell>Group</Cell><Cell rowspan=\"2\">Right</Cell></Row><Row auto-filter=\"true\"><Cell>Middle</Cell></Row>";
        byte[] bytes = new WorkbookRenderer().Render(Xml(rows), false);
        Assert.Equal("A2:C2", AutoFilterReference(bytes));
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal("Middle", book.Worksheet(1).Cell("B2").GetString());
        Assert.Equal(2, book.Worksheet(1).MergedRanges.Count);
    }

    [Fact]
    public void AutoFilterIncludesFullyInheritedMergedHeaderWithNoEmittedCells()
    {
        const string rows = "<Row><Cell rowspan=\"2\" colspan=\"3\">Shared header</Cell></Row><Row auto-filter=\"true\"/>";
        byte[] bytes = new WorkbookRenderer().Render(Xml(rows), false);
        Assert.Equal("A2:C2", AutoFilterReference(bytes));
    }

    [Fact]
    public void AutoFilterDoesNotIncludeWiderMergesThatEndedBeforeTheHeaderRow()
    {
        const string rows = "<Row><Cell rowspan=\"2\" colspan=\"5\">Earlier title</Cell></Row><Row/><Row auto-filter=\"true\"><Cell>One</Cell><Cell>Two</Cell></Row>";
        byte[] bytes = new WorkbookRenderer().Render(Xml(rows), false);
        Assert.Equal("A3:B3", AutoFilterReference(bytes));
    }

    [Fact]
    public void FooterAutoFilterAlsoUsesAnExplicitRange()
    {
        byte[] bytes = new WorkbookRenderer().Render(Xml("<Row style=\"height:400\"><Cell>body</Cell></Row>", extra: "<LastPageRow auto-filter=\"true\"><Cell>First</Cell><Cell>Second</Cell></LastPageRow>"), false);
        Assert.Equal("A2:B2", AutoFilterReference(bytes));
    }

    [Fact]
    public void EmptyAutoFilterRowDoesNotEmitAnInvalidWholeRowReference()
    {
        byte[] bytes = new WorkbookRenderer().Render(Xml("<Row auto-filter=\"true\"/><Row><Cell>data</Cell></Row>"), false);
        Assert.Null(AutoFilterReference(bytes));
    }
}
