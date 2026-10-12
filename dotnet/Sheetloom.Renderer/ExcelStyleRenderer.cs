using ClosedXML.Excel;

namespace Sheetloom;

public class ExcelStyleRenderer
{
    public static string PrepareText(string rawText)
    {
        string decoded = System.Net.WebUtility.HtmlDecode(rawText);
        return decoded
            .Replace("<br/>", "\n")
            .Replace("<br>", "\n")
            .Replace("&#xA;", "\n")
            .Replace("&lt;br/&gt;", "\n");
    }

    private static void ToExcelStyle(CssStyle cssStyle, IXLStyle excelStyle)
    {
        if (cssStyle.TryGet<double>(StyleKey.FontSize, out var fontSize))
            excelStyle.Font.FontSize = fontSize;
        if (cssStyle.TryGet<FontWeight>(StyleKey.FontWeight, out var fontWeight))
        {
            excelStyle.Font.Bold = fontWeight == FontWeight.Bold;
        }

        if (cssStyle.TryGet<string>(StyleKey.FontFamily, out var fontFamily))
        {
            excelStyle.Font.FontName = fontFamily;
        }

        if (cssStyle.TryGet<TextAlign>(StyleKey.TextAlign, out var textAlign))
        {
            excelStyle.Alignment.Horizontal = ToExcelAlignment(textAlign);
        }

        if (cssStyle.TryGet<VerticalAlign>(StyleKey.VerticalAlignment, out var verticalAlignment))
            excelStyle.Alignment.Vertical = ToExcelAlignment(verticalAlignment);

        if (cssStyle.TryGet<TextDirection>(StyleKey.TextDirection, out var textDirection))
            excelStyle.Alignment.TextRotation = textDirection == TextDirection.Vertical ? 90 : 0;

        if (cssStyle.TryGet<TextWrap>(StyleKey.TextWrap, out var textWrap))
            excelStyle.Alignment.WrapText = textWrap == TextWrap.Wrap;

        if (cssStyle.TryGet<string>(StyleKey.NumberFormat, out var format))
            excelStyle.NumberFormat.Format = format;
        if (cssStyle.TryGet<string>(StyleKey.BackgroundColor, out var backgroundColor))
            excelStyle.Fill.BackgroundColor = XLColor.FromHtml(backgroundColor);

        if (cssStyle.TryGet<string>(StyleKey.Color, out var color))
            excelStyle.Font.FontColor = XLColor.FromHtml(color);

        ApplyBorders(cssStyle, excelStyle);
    }

    private static void ApplyBorders(CssStyle cssStyle, IXLStyle excelStyle)
    {
        XLBorderStyleValues GetBorder(double val) => val switch
        {
            < 1 => XLBorderStyleValues.None,
            < 2 => XLBorderStyleValues.Thin,
            < 3 => XLBorderStyleValues.Medium,
            < 4 => XLBorderStyleValues.Thick,
            _ => XLBorderStyleValues.DashDot
        };

        void ApplyBorder(StyleKey key, Action<XLBorderStyleValues> action)
        {
            if (cssStyle.TryGet<double>(key, out var border))
            {
                action(GetBorder(border));
            }
        }

        Action<XLBorderStyleValues>
            bottom = s => excelStyle.Border.BottomBorder = s,
            top = s => excelStyle.Border.TopBorder = s,
            right = s => excelStyle.Border.RightBorder = s,
            left = s => excelStyle.Border.LeftBorder = s;

        ApplyBorder(StyleKey.Border, left + right + top + bottom);
        ApplyBorder(StyleKey.BorderBottom, bottom);
        ApplyBorder(StyleKey.BorderTop, top);
        ApplyBorder(StyleKey.BorderRight, right);
        ApplyBorder(StyleKey.BorderLeft, left);

        if (cssStyle.TryGet<string>(StyleKey.BorderColor, out var borderColor))
        {
            var color = XLColor.FromHtml(borderColor);
            excelStyle.Border.BottomBorderColor = color;
            excelStyle.Border.TopBorderColor = color;
            excelStyle.Border.RightBorderColor = color;
            excelStyle.Border.LeftBorderColor = color;
        }

        if (cssStyle.TryGet<string>(StyleKey.BorderTopColor, out var borderTopColor))
        {
            excelStyle.Border.TopBorderColor = XLColor.FromHtml(borderTopColor);
        }

        if (cssStyle.TryGet<string>(StyleKey.BorderBottomColor, out var borderBottomColor))
        {
            excelStyle.Border.BottomBorderColor = XLColor.FromHtml(borderBottomColor);
        }

        if (cssStyle.TryGet<string>(StyleKey.BorderRightColor, out var borderRightColor))
        {
            excelStyle.Border.RightBorderColor = XLColor.FromHtml(borderRightColor);
        }

        if (cssStyle.TryGet<string>(StyleKey.BorderLeftColor, out var borderLeftColor))
        {
            excelStyle.Border.LeftBorderColor = XLColor.FromHtml(borderLeftColor);
        }
    }

    private static XLAlignmentHorizontalValues ToExcelAlignment(TextAlign val)
    {
        return val switch
        {
            TextAlign.Left => XLAlignmentHorizontalValues.Left,
            TextAlign.Right => XLAlignmentHorizontalValues.Right,
            TextAlign.Center => XLAlignmentHorizontalValues.Center,
            TextAlign.Justify => XLAlignmentHorizontalValues.Justify,
            _ => XLAlignmentHorizontalValues.General
        };
    }

    private static XLAlignmentVerticalValues ToExcelAlignment(VerticalAlign val)
    {
        return val switch
        {
            VerticalAlign.Top => XLAlignmentVerticalValues.Top,
            VerticalAlign.Bottom => XLAlignmentVerticalValues.Bottom,
            VerticalAlign.Center => XLAlignmentVerticalValues.Center,
            _ => XLAlignmentVerticalValues.Bottom
        };
    }

    public static void ApplyWidth(CssStyle? style, Action<double> action)
    {
        if (style == null)
        {
            return;
        }
        if (style.Value.TryGet<double>(StyleKey.Width, out var width))
        {
            action(width);
        }
    }

    public static void ApplyHeight(CssStyle? style, Action<double> action)
    {
        if (style == null)
        {
            return;
        }
        if (style.Value.TryGet<double>(StyleKey.Height, out var height))
        {
            action(height);
        }
    }

    public static void Render(CssStyle? baseStyle, IXLStyle current)
    {
        if (baseStyle == null)
        {
            return;
        }

        ToExcelStyle(baseStyle.Value, current);
    }
}
