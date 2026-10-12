using System.Collections.Immutable;

namespace Sheetloom;

public record BaseModel(CssStyle? Style, string? ExcelRowRef, string? ExcelColumnRef);

public record WorkbookModel(
    ImmutableArray<WorksheetModel> Worksheets,
    CssStyle? Style,
    string? ExcelRowRef,
    string? ExcelColumnRef)
    : BaseModel(Style, ExcelRowRef, ExcelColumnRef);

public enum Orientation
{
    Portrait,
    Landscape
}
public enum PaperSize
{
    A4,
    A3
}

public record PrintingAreaModel(string? Range, Orientation? Orientation, PaperSize? PaperSize, string? RowsToRepeatAtTop);

public record WorksheetModel(
    string Name,
    ImmutableArray<RowModel> Rows,
    RowModel? LastPageRow,
    ImmutableArray<NamedRangeModel> NamedRanges,
    CssStyle? Style,
    string? ExcelRowRef,
    string? ExcelColumnRef,
    PrintingAreaModel PrintingArea,
    bool FitColumnsInPage,
    bool FreezePaneFromStart)
    : BaseModel(Style, ExcelRowRef, ExcelColumnRef);

public record NamedRangeModel(string Name, string Value);

public record RowModel(
    ImmutableArray<CellModel> Cells,
    bool AutoFilter,
    CssStyle? Style,
    string? ExcelRowRef,
    string? ExcelColumnRef,
    bool? RenderLastPageRow,
    bool Hide,
    bool FreezePane)
    : BaseModel(Style, ExcelRowRef, ExcelColumnRef);

public record CellModel(
    string Text,
    DataType DataType,
    int? Colspan,
    int? Rowspan,
    string? Formula,
    string? ImageSource,
    int? ImageWidth,
    int? ImageHeight,
    CssStyle? Style,
    bool FitColumn,
    bool FitRow,
    string? ExcelRowRef,
    string? ExcelColumnRef,
    string? ExcelNamed,
    bool WrapText)
    : BaseModel(Style, ExcelRowRef, ExcelColumnRef);
