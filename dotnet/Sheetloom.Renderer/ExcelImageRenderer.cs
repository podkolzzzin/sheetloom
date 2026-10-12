using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ClosedXML.Graphics;

namespace Sheetloom;

internal sealed class ExcelImageRenderer
{
    private readonly List<(IXLPicture Picture, IXLRange Range)> pendingImages = [];

    public void CompleteLayout()
    {
        foreach (var (picture, range) in pendingImages)
        {
            Position(picture, range);
        }
    }

    public void Add(IXLWorksheet excelWorksheet, IXLCell xlCell, CellModel cell)
    {
        var bytes = TryDecodeImage(cell.ImageSource);
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        using var stream = new MemoryStream(bytes);
        var picture = excelWorksheet.AddPicture(stream);

        // Get the cell or merged range
        var targetRange = xlCell.IsMerged() ? xlCell.MergedRange() : xlCell.AsRange();

        pendingImages.Add((picture, targetRange));
    }

    internal static void Position(IXLPicture picture, IXLRange range)
    {
        var last = range.LastCell();
        int lastRow = last.Address.RowNumber;
        int lastColumn = last.Address.ColumnNumber;
        int endRow = Math.Min(lastRow + 1, XLHelper.MaxRowNumber);
        int endColumn = Math.Min(lastColumn + 1, XLHelper.MaxColumnNumber);
        int offsetX = 0;
        int offsetY = 0;
        // The usual marker is the next cell's top-left corner. At the sheet edge use a
        // positive offset within the last legal marker, never an out-of-bounds cell.
        if (lastColumn == XLHelper.MaxColumnNumber)
        {
            var fallbackFont = Environment.GetEnvironmentVariable("RENDERER_FALLBACK_FONT") ?? "DejaVu Sans";
            var engine = new DefaultGraphicEngine(fallbackFont);
            int digitWidth = (int)Math.Round(
                engine.GetMaxDigitWidth(range.Worksheet.Workbook.Style.Font, 96),
                MidpointRounding.AwayFromZero);
            int padding = 2 * (int)Math.Ceiling(digitWidth / 4.0) + 1;
            double width = last.WorksheetColumn().Width;
            double pixelWidth = width < 1
                ? width * (digitWidth + padding)
                : width * digitWidth + padding;
            offsetX = (int)Math.Round(pixelWidth, MidpointRounding.AwayFromZero);
        }

        if (lastRow == XLHelper.MaxRowNumber)
        {
            offsetY = (int)Math.Round(last.WorksheetRow().Height * 96 / 72, MidpointRounding.AwayFromZero);
        }

        picture.MoveTo(range.FirstCell(), 0, 0, range.Worksheet.Cell(endRow, endColumn), offsetX, offsetY);
        picture.Placement = XLPicturePlacement.MoveAndSize;
    }

    private static byte[]? TryDecodeImage(string? imageSource)
    {
        if (string.IsNullOrWhiteSpace(imageSource))
        {
            return null;
        }

        const string base64Marker = "base64,";
        var base64Index = imageSource.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
        if (base64Index < 0)
        {
            return null;
        }

        var base64 = imageSource[(base64Index + base64Marker.Length)..];
        return Convert.FromBase64String(base64);
    }
}
