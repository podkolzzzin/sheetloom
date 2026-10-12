using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using static Sheetloom.Tests.WorkbookFixture;

namespace Sheetloom.Tests;

public sealed class WorkbookIsolationTests
{
    static WorkbookIsolationTests() => ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

    [Fact]
    public void ReferenceScopeSpansWorksheetsWithinOneWorkbook()
    {
        using var book = Open("<Workbook><Worksheet name=\"First\"><Row><Cell>x</Cell><Cell excel-column-ref=\"legacyColumn\">y</Cell></Row></Worksheet><Worksheet name=\"Second\"><Row><Cell><FormulaR1C1>RC{legacyColumn}</FormulaR1C1></Cell><Cell data-type=\"Number\">8</Cell></Row></Worksheet></Workbook>");
        Assert.Equal("RC2", book.Worksheet(2).Cell("A1").FormulaR1C1);
        Assert.Equal(8, book.Worksheet(2).Cell("A1").GetDouble());
    }

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

    [Fact]
    public void CaseInsensitiveDuplicateSheetNamesFailThenSameRendererRecovers()
    {
        var renderer = new WorkbookRenderer();
        Assert.ThrowsAny<Exception>(() => renderer.Render("<Workbook><Worksheet name=\"Report\"/><Worksheet name=\"report\"/></Workbook>", false));
        using var recovered = new XLWorkbook(new MemoryStream(renderer.Render(Xml("<Row><Cell>next request</Cell></Row>"), false)));
        Assert.Equal("next request", recovered.Worksheet(1).Cell("A1").GetString());
    }

    [Fact]
    public async Task ConcurrentRequestsWithDifferentReferencePositionsRemainIsolated()
    {
        var renderer = new WorkbookRenderer();
        await Task.WhenAll(Enumerable.Range(1, 20).Select(column => Task.Run(() =>
        {
            string padding = string.Concat(Enumerable.Repeat("<Cell/>", column - 1));
            string xml = Xml($"<Row>{padding}<Cell excel-column-ref=\"amount\" data-type=\"Number\">{column * 3}</Cell><Cell><FormulaR1C1>RC{{amount}}</FormulaR1C1></Cell></Row>");
            using var book = new XLWorkbook(new MemoryStream(renderer.Render(xml, false)));
            Assert.Equal($"RC{column}", book.Worksheet(1).Cell(1, column + 1).FormulaR1C1);
            Assert.Equal(column * 3, book.Worksheet(1).Cell(1, column + 1).GetDouble());
        })));
    }
}
