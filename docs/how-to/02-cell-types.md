# Preserve cell types and apply styles

[← README](../../README.md) · Place this fragment inside `<Worksheet>`:

```vue
<Row>
  <Cell data-type="Text" style="width:18">001234</Cell>
  <Cell data-type="Number" style="number-format:0.00">12.5</Cell>
  <Cell data-type="DateOnly" style="number-format:dd.mm.yyyy">2026-10-12</Cell>
  <Cell data-type="Boolean">true</Cell>
</Row>
<Row>
  <Cell colspan="4" style="text-wrap:wrap;border:1">
    A wrapped note spanning four columns.
  </Cell>
</Row>
```

**Result:** the identifier keeps its leading zeros, the amount stays numeric and the date is stored as a date. The final cell spans four columns.

- The default type is `Text`. Use it for identifiers and account numbers.
- Use a decimal point for numbers and `yyyy-MM-dd` for dates. Invalid values remain text; empty values become blank cells. Formatting changes display, not type.
- `style` is a limited renderer language, without CSS classes or `px`. Width uses Excel column units; height uses points.
- Do not add placeholder cells for positions occupied by `colspan` or `rowspan`. The renderer finds available space automatically.

See the [reference](../workbook-format.md) for all properties and limitations of formats containing `:` or `;`.
