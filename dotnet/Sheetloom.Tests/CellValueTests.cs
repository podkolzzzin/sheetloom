using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class CellValueTests
{
    static CellValueTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

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
    [InlineData("Number")]
    [InlineData("Boolean")]
    [InlineData("DateOnly")]
    [InlineData("Text")]
    public void EmptyTypedValuesStayBlank(string type)
    {
        using var book = Open(Xml($"<Row><Cell data-type=\"{type}\"/><Cell>anchor</Cell></Row>"));
        Assert.Equal(XLDataType.Blank, book.Worksheet(1).Cell("A1").DataType);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t\n ")]
    public void NonemptyWhitespaceTextSurvivesParsingAndSerialization(string text)
    {
        string xml = Xml($"<Row><Cell data-type=\"Text\">{text}</Cell><Cell>marker</Cell></Row>");
        var model = new global::Sheetloom.WorkbookParser().Load(xml);
        Assert.Equal(text, model.Worksheets[0].Rows[0].Cells[0].Text);
        using var book = Open(xml);
        Assert.Equal(XLDataType.Text, book.Worksheet(1).Cell("A1").DataType);
        Assert.Equal(text, book.Worksheet(1).Cell("A1").GetString());
        Assert.Equal("marker", book.Worksheet(1).Cell("B1").GetString());
    }

    [Theory]
    [InlineData("'s-Hertogenbosch")]
    [InlineData("'t Hooft")]
    [InlineData("'=1+2")]
    [InlineData("''double")]
    [InlineData("'")]
    public void LeadingApostropheIsTextContentRatherThanQuotePrefixMetadata(string text)
    {
        using var book = Open(Xml($"<Row><Cell data-type=\"Text\">{text}</Cell></Row>"));
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
        using var book = Open($"<Workbook style=\"font-family:DejaVu Sans\"><Worksheet name=\"Report\" style=\"font-size:18\"><Row style=\"font-weight:bold\"><Cell style=\"color:#ff0000\">{text}</Cell></Row></Worksheet></Workbook>");
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
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    public void IsoDatesRemainGregorianUnderNonGregorianHostCultures(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var book = Open(Xml("<Row><Cell data-type=\"DateOnly\">2024-02-29</Cell></Row>"));
            Assert.Equal(XLDataType.DateTime, book.Worksheet(1).Cell("A1").DataType);
            Assert.Equal(new DateTime(2024, 2, 29), book.Worksheet(1).Cell("A1").GetDateTime());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e9999")]
    public void NonFiniteNumericInputFallsBackToTextLikeOtherInvalidNumbers(string input)
    {
        using var book = Open(Xml($"<Row><Cell data-type=\"Number\">{input}</Cell><Cell data-type=\"Number\">5</Cell></Row>"));
        Assert.Equal(XLDataType.Text, book.Worksheet(1).Cell("A1").DataType);
        Assert.Equal(input, book.Worksheet(1).Cell("A1").GetString());
        Assert.Equal(5, book.Worksheet(1).Cell("B1").GetDouble());
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+123")]
    [InlineData("@SUM(A1)")]
    [InlineData("000123")]
    public void TextLookingLikeFormulaOrIdentifierRemainsLiteral(string text)
    {
        using var book = Open(Xml($"<Row><Cell>{text}</Cell></Row>"));
        var cell = book.Worksheet(1).Cell("A1");
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal(text, cell.GetString());
        Assert.False(cell.HasFormula);
    }
}
