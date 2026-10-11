using System.Collections.Immutable;

namespace Sheetloom;

public enum StyleKey
{
    FontSize, FontWeight,
    FontFamily,
    TextAlign,
    Height,
    Width,
    TextDirection,
    TextWrap,
    VerticalAlignment,
    BorderBottom,
    BorderTop,
    BorderRight,
    BorderLeft,
    Border,
    NumberFormat,
    BorderColor,
    BorderLeftColor,
    BorderRightColor,
    BorderBottomColor,
    BorderTopColor,
    BackgroundColor,
    Color,
    BreakAfter
}

public enum BreakStyle
{
    Always
}

public enum FontWeight
{
    Bold, Normal
}

public enum TextAlign
{
    Left, Right, Center, Justify
}

public enum VerticalAlign
{
    Top, Bottom, Center
}

public enum TextDirection
{
    Horizontal, Vertical
}

public enum TextWrap
{
    NoWrap, Wrap
}

public readonly struct CssStyle
{
    private record StyleValue(string Value);
    private static readonly Dictionary<string, StyleKey> _styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["font-size"] = StyleKey.FontSize,
        ["font-weight"] = StyleKey.FontWeight,
        ["font-family"] = StyleKey.FontFamily,
        ["text-align"] = StyleKey.TextAlign,
        ["height"] = StyleKey.Height,
        ["width"] = StyleKey.Width,
        ["text-direction"] = StyleKey.TextDirection,
        ["text-wrap"] = StyleKey.TextWrap,
        ["vertical-align"] = StyleKey.VerticalAlignment,
        ["border-bottom"] = StyleKey.BorderBottom,
        ["border-top"] = StyleKey.BorderTop,
        ["border-right"] = StyleKey.BorderRight,
        ["border-left"] = StyleKey.BorderLeft,
        ["border"] = StyleKey.Border,
        ["number-format"] = StyleKey.NumberFormat,
        ["border-color"] = StyleKey.BorderColor,
        ["border-left-color"] = StyleKey.BorderLeftColor,
        ["border-right-color"] = StyleKey.BorderRightColor,
        ["border-top-color"] = StyleKey.BorderTopColor,
        ["border-bottom-color"] = StyleKey.BorderBottomColor,
        ["background-color"] = StyleKey.BackgroundColor,
        ["color"] = StyleKey.Color,
        ["break-after"] = StyleKey.BreakAfter,
    };

    private readonly ImmutableDictionary<StyleKey, StyleValue> _values;

    private CssStyle(ImmutableDictionary<StyleKey, StyleValue> values) => _values = values;

    public CssStyle Merge(CssStyle value)
    {
        var values = _values.ToBuilder();
        foreach (var (k, v) in value._values)
        {
            values[k] = v;
        }

        return new CssStyle(values.ToImmutable());
    }

    public CssStyle Merge(string? value)
    {
        var items = value?.Split(";") ?? [];
        var values = _values.ToBuilder();
        foreach (var item in items)
        {
            var r = item.Split(":").Select(x => x.Trim()).ToArray();
            var k = r.First();
            var v = r.Last();
            if (_styles.TryGetValue(k, out var key))
            {
                values[key] = new StyleValue(v);
            }
        }

        return new CssStyle(values.ToImmutable());
    }

    public bool TryGet<T>(StyleKey key, out T val)
    {
        val = default!;
        if (_values.TryGetValue(key, out var value))
        {
            if (typeof(T).IsEnum)
            {
                val = (T)Enum.Parse(typeof(T), value.Value, true);
            }
            else
            {
                val = (T)Convert.ChangeType(value.Value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            }

            return true;
        }
        return false;
    }

    public CssStyle(string? value)
    {
        var builder = ImmutableDictionary.CreateBuilder<StyleKey, StyleValue>();
        var items = value?.Split(";") ?? [];
        foreach (var item in items)
        {
            var r = item.Split(":").Select(x => x.Trim()).ToArray();
            var k = r.First();
            var v = r.Last();
            if (_styles.TryGetValue(k, out var key))
            {
                builder[key] = new StyleValue(v);
            }
        }
        _values = builder.ToImmutable();
    }
}