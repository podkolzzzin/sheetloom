using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class WorkbookSerializationTests
{
    static WorkbookSerializationTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrecisionOptionChangesSerializedCalculationSettingOnly(bool precision)
    {
        var bytes = new WorkbookRenderer().Render(Xml("<Row><Cell data-type=\"Number\" style=\"number-format:0.00\">1.2345</Cell></Row>"), precision);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var xml = archive.GetEntry("xl/workbook.xml")!.Open();
        var doc = XDocument.Load(xml);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string? fullPrecision = doc.Root!.Element(ns + "calcPr")?.Attribute("fullPrecision")?.Value;
        if (precision)
            Assert.Contains(fullPrecision, new[] { "0", "false" });
        else
            Assert.DoesNotContain(fullPrecision, new[] { "0", "false" });
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(1.2345, book.Worksheet(1).Cell(1, 1).GetDouble());
    }

    [Theory]
    [InlineData("<Workbook>")]
    [InlineData("<NotAWorkbook/>")]
    [InlineData("<!DOCTYPE Workbook [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><Workbook><Worksheet name='x'><Row><Cell>&xxe;</Cell></Row></Worksheet></Workbook>")]
    [InlineData("<!DOCTYPE Workbook [<!ENTITY a 'test'>]><Workbook/>")]
    public void MalformedXmlAndDtdAreRejected(string xml) => Assert.ThrowsAny<Exception>(() => new WorkbookRenderer().Render(xml, false));

    [Fact]
    public void PrecisionOptionDoesNotDropFormulasMergesStylesOrOtherCalculationAttributes()
    {
        string xml = Xml("<Row><Cell data-type=\"Number\" style=\"number-format:0.00\">1.2345</Cell><Cell colspan=\"2\">merged</Cell></Row><Row><Cell><FormulaR1C1>R1C1*2</FormulaR1C1></Cell></Row>");
        byte[] normal = Render(xml);
        byte[] rounded = Render(xml, true);
        foreach (string path in new[] { "xl/worksheets/sheet1.xml", "xl/styles.xml", "xl/sharedStrings.xml" })
            Assert.True(XNode.DeepEquals(Part(normal, path), Part(rounded, path)), path);
        var normalWorkbook = Part(normal, "xl/workbook.xml");
        var roundedWorkbook = Part(rounded, "xl/workbook.xml");
        var precision = roundedWorkbook.Descendants(Spreadsheet + "calcPr").Single().Attribute("fullPrecision")!;
        Assert.Contains(precision.Value, new[] { "0", "false" });
        precision.Remove();
        normalWorkbook.Descendants(Spreadsheet + "calcPr").Single().Attribute("fullPrecision")?.Remove();
        Assert.True(XNode.DeepEquals(normalWorkbook, roundedWorkbook));
    }

    [Theory]
    [InlineData("<SecondRoot/>")]
    [InlineData("trailing garbage")]
    public void TrailingMalformedXmlIsNotSilentlyIgnored(string suffix) =>
        Assert.ThrowsAny<Exception>(() => new WorkbookRenderer().Render(Xml("<Row><Cell>valid prefix</Cell></Row>") + suffix, false));

    [Fact]
    public void ValidEmptyRowsAndWorksheetsDoNotLoseFollowingData()
    {
        using var book = Open("<Workbook><Worksheet name=\"Empty\"/><Worksheet name=\"Data\"><Row/><Row><Cell/><Cell>second column</Cell></Row></Worksheet></Workbook>");
        Assert.Equal(2, book.Worksheets.Count);
        Assert.Equal("second column", book.Worksheet("Data").Cell("B2").GetString());
        Assert.Equal(XLDataType.Blank, book.Worksheet("Data").Cell("A2").DataType);
    }
}
