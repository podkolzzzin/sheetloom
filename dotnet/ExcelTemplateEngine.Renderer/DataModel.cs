using System.Xml.Serialization;

namespace ExcelTemplateEngine;


public class Node
{
    [XmlAttribute("style")]
    public string? Style { get; set; }

    [XmlAttribute("excel-column-ref")]
    public string? ExcelColumnRef { get; set; }

    [XmlAttribute("excel-row-ref")]
    public string? ExcelRowRef { get; set; }

    [XmlAttribute("excel-named")]
    public string? ExcelNamed { get; set; }
}

[XmlRoot("Workbook")]
public class Workbook : Node
{
    [XmlElement("Worksheet")]
    public List<Worksheet> Worksheets { get; set; } = [];
}

public class PrintingArea
{
    [XmlAttribute("orientation")]
    public Orientation Orientation { get; set; }

    [XmlAttribute("paper-size")]
    public PaperSize PaperSize { get; set; }

    [XmlElement("RowsToRepeatAtTop")]
    public string? RowsToRepeatAtTop { get; set; }

    [XmlText]
    public string Range { get; set; } = string.Empty;
}

public class Worksheet : Node
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Row")]
    public List<Row> Rows { get; set; } = [];

    [XmlAttribute("fit-columns-to-page")]
    public bool FitColumnsToPage { get; set; }

    [XmlAttribute("freeze-pane-from-start")]
    public bool FreezePaneFromStart { get; set; }

    [XmlElement("PrintingArea")]
    public PrintingArea? PrintingArea { get; set; }

    [XmlElement("NamedRange")]
    public List<NamedRange> NamedRanges { get; set; } = [];

    [XmlElement("LastPageRow")]
    public Row? LastPageRow { get; set; }
}

public class NamedRange
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlText]
    public string Value { get; set; } = string.Empty;
}

public class Row : Node
{
    [XmlElement("Cell")]
    public List<Cell> Cells { get; set; } = [];

    [XmlAttribute("auto-filter")]
    public bool AutoFilter { get; set; }

    [XmlAttribute("hide")]
    public bool Hide { get; set; }

    [XmlAttribute("render-last-page-row")]

    public string? RenderLastPageRowAsText { get; set; }

    public bool? RenderLastPageRow => string.IsNullOrEmpty(RenderLastPageRowAsText) ? null : bool.Parse(RenderLastPageRowAsText);

    [XmlAttribute("freeze-pane")]
    public bool FreezePane { get; set; }
}

public class Cell : Node
{
    [XmlText]
    public string Text { get; set; } = string.Empty;

    [XmlAttribute("colspan")]
    public string? Colspan { get; set; }

    [XmlAttribute("rowspan")]
    public string? Rowspan { get; set; }

    [XmlAttribute("data-type")]
    public string? DataType { get; set; }

    [XmlAttribute("image-src")]
    public string? ImageSrc { get; set; }

    [XmlAttribute("image-width")]
    public string? ImageWidth { get; set; }

    [XmlAttribute("image-height")]
    public string? ImageHeight { get; set; }

    [XmlElement("FormulaR1C1")]
    public string? FormulaR1C1 { get; set; }

    [XmlAttribute("fit-column")]
    public bool FitColumn { get; set; }

    [XmlAttribute("fit-row")]
    public bool FitRow { get; set; }

    [XmlAttribute("wrap-text")]
    public string? WrapText { get; set; }
}

public enum DataType { Text, Number, DateOnly, Boolean }
