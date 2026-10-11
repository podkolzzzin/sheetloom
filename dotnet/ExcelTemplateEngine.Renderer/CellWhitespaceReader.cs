using System.Xml;

namespace ExcelTemplateEngine;

/// <summary>Preserve whitespace-only cell values without treating document indentation as data.</summary>
internal sealed class CellWhitespaceReader(XmlReader inner) : XmlReader
{
    private int cellDepth = -1;
    public override bool Read()
    {
        bool available = inner.Read();
        if (inner.NodeType == XmlNodeType.Element && inner.LocalName == "Cell" && !inner.IsEmptyElement)
            cellDepth = inner.Depth;
        else if (inner.NodeType == XmlNodeType.EndElement && inner.Depth == cellDepth)
            cellDepth = -1;
        return available;
    }

    // XmlSerializer ignores ordinary XmlWhitespace even when IgnoreWhitespace=false.
    public override XmlNodeType NodeType => inner.NodeType == XmlNodeType.Whitespace &&
        cellDepth >= 0 && inner.Depth == cellDepth + 1 ? XmlNodeType.Text : inner.NodeType;
    public override int AttributeCount => inner.AttributeCount;
    public override string BaseURI => inner.BaseURI;
    public override int Depth => inner.Depth;
    public override bool EOF => inner.EOF;
    public override bool HasValue => inner.HasValue;
    public override bool IsEmptyElement => inner.IsEmptyElement;
    public override string LocalName => inner.LocalName;
    public override string Name => inner.Name;
    public override string NamespaceURI => inner.NamespaceURI;
    public override XmlNameTable NameTable => inner.NameTable;
    public override string Prefix => inner.Prefix;
    public override ReadState ReadState => inner.ReadState;
    public override string Value => inner.Value;
    public override string XmlLang => inner.XmlLang;
    public override XmlSpace XmlSpace => inner.XmlSpace;
    public override string GetAttribute(int i) => inner.GetAttribute(i);
    public override string? GetAttribute(string name) => inner.GetAttribute(name);
    public override string? GetAttribute(string name, string? namespaceURI) => inner.GetAttribute(name, namespaceURI);
    public override string? LookupNamespace(string prefix) => inner.LookupNamespace(prefix);
    public override bool MoveToAttribute(string name) => inner.MoveToAttribute(name);
    public override bool MoveToAttribute(string name, string? ns) => inner.MoveToAttribute(name, ns);
    public override void MoveToAttribute(int i) => inner.MoveToAttribute(i);
    public override bool MoveToElement() => inner.MoveToElement();
    public override bool MoveToFirstAttribute() => inner.MoveToFirstAttribute();
    public override bool MoveToNextAttribute() => inner.MoveToNextAttribute();
    public override bool ReadAttributeValue() => inner.ReadAttributeValue();
    public override void ResolveEntity() => inner.ResolveEntity();
    public override void Close() => inner.Close();
}
