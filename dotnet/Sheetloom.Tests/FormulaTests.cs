using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class FormulaTests
{
    static FormulaTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

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
        byte[] bytes = Render(Xml($"<Row excel-row-ref=\"row rows\"><Cell excel-column-ref=\"amount\" data-type=\"Number\">3</Cell><Cell excel-column-ref=\"amountTotal\" data-type=\"Number\">7</Cell></Row><Row><Cell><FormulaR1C1>{reference}+R{{rows}}C{{amountTotal}}</FormulaR1C1></Cell></Row>"));
        var cell = Part(bytes, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "c").Single(c => c.Attribute("r")?.Value == "A2");
        Assert.Equal("$A$1+$B$1", cell.Element(Spreadsheet + "f")!.Value);
        Assert.DoesNotContain("{", cell.Element(Spreadsheet + "f")!.Value);
    }
}
