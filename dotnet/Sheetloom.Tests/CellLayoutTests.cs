using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class CellLayoutTests
{
    static CellLayoutTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

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
        var sheet = book.Worksheet(1);
        var cell = sheet.Cell("A1");
        var style = cell.Style;
        Assert.Equal("DejaVu Sans", style.Font.FontName);
        Assert.Equal(16, style.Font.FontSize);
        Assert.True(style.Font.Bold);
        Assert.Equal(XLAlignmentHorizontalValues.Center, style.Alignment.Horizontal);
        Assert.Equal(XLAlignmentVerticalValues.Top, style.Alignment.Vertical);
        Assert.Equal(90, style.Alignment.TextRotation);
        Assert.True(style.Alignment.WrapText);
        Assert.Equal("0.00", style.NumberFormat.Format);
        Assert.Equal("FFFFFF00", style.Fill.BackgroundColor.Color.ToArgb().ToString("X8"));
        Assert.Equal("FFFF0000", style.Font.FontColor.Color.ToArgb().ToString("X8"));
        Assert.Equal(XLBorderStyleValues.Medium, style.Border.TopBorder);
        Assert.Equal(XLBorderStyleValues.Thick, style.Border.BottomBorder);
        Assert.Equal(XLBorderStyleValues.None, style.Border.LeftBorder);
        Assert.Equal(XLBorderStyleValues.DashDot, style.Border.RightBorder);
        Assert.Equal(XLColor.FromHtml("#00ff00"), style.Border.TopBorderColor);
        Assert.Equal(XLColor.FromHtml("#0000ff"), style.Border.BottomBorderColor);
        Assert.Equal(30, sheet.Column(1).Width, 2);
        Assert.True(sheet.Row(1).Height >= 35);
    }

    [Fact]
    public void FitColumnAndWrappedRowIncreaseDimensions()
    {
        using var book = Open(Xml("<Row><Cell fit-column=\"true\">A fairly long column heading</Cell></Row><Row><Cell style=\"width:10\" wrap-text=\"true\">This long line must wrap across many lines in a narrow column.</Cell></Row>"));
        Assert.True(book.Worksheet(1).Row(2).Height > 30);
    }

    [Fact]
    public void DeferredFitUsesFinalColumnWidthInsteadOfFirstRowWidth()
    {
        string longText = new('x', 70);
        using var late = Open(Xml($"<Row><Cell wrap-text=\"true\">{longText}</Cell></Row><Row><Cell style=\"width:40\">width setter</Cell></Row>"));
        using var early = Open(Xml($"<Row><Cell wrap-text=\"true\" style=\"width:40\">{longText}</Cell></Row><Row><Cell>width setter</Cell></Row>"));
        Assert.Equal(early.Worksheet(1).Row(1).Height, late.Worksheet(1).Row(1).Height, 3);
        Assert.InRange(late.Worksheet(1).Row(1).Height, 25, 40);
    }

    [Fact]
    public void MergedWrappedCellUsesAllFinalColumnWidthsAndPreservesExplicitMinimumHeight()
    {
        string text = new('x', 60);
        using var merged = Open(Xml($"<Row><Cell colspan=\"2\" wrap-text=\"true\">{text}</Cell></Row><Row><Cell style=\"width:20\">a</Cell><Cell style=\"width:20\">b</Cell></Row><Row style=\"height:90\"><Cell wrap-text=\"true\">short</Cell></Row>"));
        using var narrow = Open(Xml($"<Row><Cell wrap-text=\"true\">{text}</Cell></Row><Row><Cell style=\"width:20\">a</Cell></Row>"));
        Assert.True(merged.Worksheet(1).Row(1).Height < narrow.Worksheet(1).Row(1).Height);
        Assert.Equal(90, merged.Worksheet(1).Row(3).Height);
    }

    [Fact]
    public void InheritedFontFamilyAndExplicitNewlinesAffectWrappedHeight()
    {
        string Build(string font) => $"<Workbook style=\"font-family:{font};font-size:11\"><Worksheet name=\"Report\"><Row><Cell wrap-text=\"true\" style=\"width:20\">{new string('x', 100)}</Cell></Row><Row><Cell wrap-text=\"true\" style=\"width:100\">first&lt;br/&gt;second&lt;br/&gt;third</Cell></Row></Worksheet></Workbook>";
        using var narrow = Open(Build("Times New Roman"));
        using var wide = Open(Build("Courier New"));
        Assert.True(wide.Worksheet(1).Row(1).Height > narrow.Worksheet(1).Row(1).Height);
        Assert.True(narrow.Worksheet(1).Row(2).Height > 40);
        Assert.Equal("first\nsecond\nthird", narrow.Worksheet(1).Cell("A2").GetString());
    }

    [Fact]
    public void SpanningCellFindsAWholeFreeRectangleAfterAnEarlierRowspan()
    {
        const string rows = "<Row><Cell>first</Cell><Cell rowspan=\"2\">vertical</Cell></Row><Row><Cell colspan=\"2\" excel-column-ref=\"horizontal\" excel-named=\"Placed\">horizontal</Cell><Cell>last</Cell></Row><Row><Cell><FormulaR1C1>R2C{horizontal}</FormulaR1C1></Cell></Row>";
        using var book = Open(Xml(rows));
        var sheet = book.Worksheet(1);
        var ranges = sheet.MergedRanges.ToArray();
        for (int i = 0; i < ranges.Length; i++)
            for (int j = i + 1; j < ranges.Length; j++)
                Assert.False(ranges[i].Intersects(ranges[j]));
        Assert.Equal("vertical", sheet.Cell("B1").GetString());
        Assert.Equal("horizontal", sheet.Cell("C2").GetString());
        Assert.Equal("last", sheet.Cell("E2").GetString());
        Assert.Equal("R2C3", sheet.Cell("A3").FormulaR1C1);
        Assert.Equal("C2", sheet.DefinedName("Placed").Ranges.Single().FirstCell().Address.ToString());
    }

    [Theory]
    [InlineData("Workbook")]
    [InlineData("Worksheet")]
    [InlineData("Row")]
    public void InheritedWrapFitsTheSameAsEffectiveCellWrap(string scope)
    {
        const string text = "The quick brown fox jumps over the lazy dog.";
        string Xml(string wrapScope, bool overrideNowrap = false) =>
            $"<Workbook style=\"font-size:14;{(wrapScope == "Workbook" ? "text-wrap:wrap" : "")}\"><Worksheet name=\"Report\" style=\"{(wrapScope == "Worksheet" ? "text-wrap:wrap" : "")}\"><Row style=\"{(wrapScope == "Row" ? "text-wrap:wrap" : "")}\"><Cell style=\"width:10;{(overrideNowrap ? "text-wrap:nowrap" : wrapScope == "Cell" ? "text-wrap:wrap" : "")}\">{text}</Cell></Row></Worksheet></Workbook>";
        using var inherited = Open(Xml(scope));
        using var explicitWrap = Open(Xml("Cell"));
        using var overridden = Open(Xml(scope, true));
        Assert.Equal(explicitWrap.Worksheet(1).Row(1).Height, inherited.Worksheet(1).Row(1).Height, 3);
        Assert.True(inherited.Worksheet(1).Row(1).Height > 50);
        Assert.False(overridden.Worksheet(1).Cell("A1").Style.Alignment.WrapText);
        Assert.True(overridden.Worksheet(1).Row(1).Height < inherited.Worksheet(1).Row(1).Height);
    }
}
