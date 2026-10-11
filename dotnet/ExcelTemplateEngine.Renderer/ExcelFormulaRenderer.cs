using ClosedXML.Excel;

using System.Text.RegularExpressions;

namespace ExcelTemplateEngine;


public class ExcelFormulaRendererInstance
{
    private static readonly Regex ReferencePattern = new(
        @"(?:(?<axis>[RC])\s*)?\{\s*(?<name>[^{}\s]+)\s*\}",
        RegexOptions.Compiled);

    private readonly Dictionary<string, int> _names = new();

    public static void Track(BaseModel model, IXLWorksheet worksheet) { }

    public void Track(BaseModel model, IXLRow row)
    {
        if (model.ExcelRowRef != null)
            Track(model.ExcelRowRef, row.RowNumber());
        Track("currentRow", row.RowNumber());
    }

    public void Track(BaseModel model, IXLCell cell)
    {
        if (model.ExcelColumnRef != null)
            Track(model.ExcelColumnRef, cell.Address.ColumnNumber);
        if (model.ExcelRowRef != null)
            Track(model.ExcelRowRef, cell.Address.RowNumber);
        Track("currentRow", cell.Address.RowNumber);
        Track("currentColumn", cell.Address.ColumnNumber);
    }

    public void Track(string names, int value)
    {
        foreach (var n in names.Split(' '))
        {
            _names[n] = value;
        }
    }

    public string Render(string formula)
    {
        return ReferencePattern.Replace(formula, match =>
        {
            if (!_names.TryGetValue(match.Groups["name"].Value, out var value))
                return match.Value;

            return $"{match.Groups["axis"].Value}{value}";
        });
    }
}


public class ExcelFormulaRenderer
{
    public static ExcelFormulaRendererInstance Create() => new();
}
