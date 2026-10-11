using System.Collections.Immutable;
using System.Xml.Serialization;
using System.Xml;

namespace Sheetloom;

public class WorkbookParser
{
    private static readonly XmlSerializer Serializer = new(typeof(Workbook));

    public WorkbookModel Load(string result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        using var textReader = new StringReader(result);
        using var reader = XmlReader.Create(textReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 8 * 1024 * 1024
        });
        using var cellReader = new CellWhitespaceReader(reader);
        var workbook = (Workbook)Serializer.Deserialize(cellReader)!;
        return FromWorkbook(workbook);
    }

    private WorkbookModel FromWorkbook(Workbook workbook)
    {
        var worksheets = workbook.Worksheets.Select(FromWorksheet).ToImmutableArray();
        var style = GetStyle(workbook.Style);
        return new WorkbookModel(worksheets, style, workbook.ExcelRowRef, workbook.ExcelColumnRef);
    }

    private WorksheetModel FromWorksheet(Worksheet worksheet)
    {
        var rows = worksheet.Rows.Select(FromRow).ToImmutableArray();
        var namedRanges = worksheet.NamedRanges.Select(x => new NamedRangeModel(x.Name, x.Value)).ToImmutableArray();
        CssStyle? style = GetStyle(worksheet.Style);
        return new WorksheetModel(
            worksheet.Name,
            rows,
            worksheet.LastPageRow == null ? null : FromRow(worksheet.LastPageRow),
            namedRanges,
            style,
            worksheet.ExcelRowRef,
            worksheet.ExcelColumnRef,
            new PrintingAreaModel(
                worksheet.PrintingArea?.Range,
                worksheet.PrintingArea?.Orientation,
                worksheet.PrintingArea?.PaperSize,
                worksheet.PrintingArea?.RowsToRepeatAtTop
            ),
            worksheet.FitColumnsToPage,
            worksheet.FreezePaneFromStart
        );
    }

    private RowModel FromRow(Row arg)
    {
        var cells = arg.Cells.Select(FromCell).ToImmutableArray();
        CssStyle? style = GetStyle(arg.Style);
        return new RowModel(cells, arg.AutoFilter, style, arg.ExcelRowRef, arg.ExcelColumnRef, arg.RenderLastPageRow,
            arg.Hide, arg.FreezePane);
    }

    private CellModel FromCell(Cell arg)
    {
        CssStyle? style = arg.Style == null ? null : new CssStyle(arg.Style);
        Enum.TryParse<DataType>(arg.DataType, true, out var type);
        var wrapText = !string.IsNullOrEmpty(arg.WrapText) && bool.TryParse(arg.WrapText, out var result) && result;
        return new CellModel(
            arg.Text,
            type,
            GetInt(arg.Colspan),
            GetInt(arg.Rowspan),
            arg.FormulaR1C1,
            arg.ImageSrc,
            GetInt(arg.ImageWidth),
            GetInt(arg.ImageHeight),
            style,
            arg.FitColumn,
            arg.FitRow,
            arg.ExcelRowRef,
            arg.ExcelColumnRef,
            arg.ExcelNamed,
            wrapText
        );
    }

    private static CssStyle? GetStyle(string? value) => value == null ? null : new CssStyle(value);

    private static int? GetInt(string? value) => value is not null ? int.Parse(value) : null;
}
