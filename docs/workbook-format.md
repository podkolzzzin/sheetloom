# Workbook XML reference

The public service renders a trusted Vue component into this XML dialect, then writes an XLSX workbook. This is an application format, not SpreadsheetML, HTML, or the Excel OOXML package format. Element and attribute names are case-sensitive. Use one `Workbook` root without an XML namespace.

## Minimal workbook and Vue template

```xml
<Workbook style="font-family:DejaVu Sans;font-size:11">
  <Worksheet name="Items">
    <Row style="font-weight:bold">
      <Cell style="width:30">Item</Cell>
      <Cell style="width:14">Amount</Cell>
    </Row>
    <Row>
      <Cell>Notebook &amp; pencil</Cell>
      <Cell data-type="Number" style="number-format:0.00">12.50</Cell>
    </Row>
  </Worksheet>
</Workbook>
```

Collections are direct repeated elements: `Workbook/Worksheet`, `Worksheet/Row`, `Row/Cell`, and `Worksheet/NamedRange`. Do not wrap them in `Worksheets`, `Rows`, `Cells`, or `NamedRanges` elements. Those wrappers are unknown and can be silently ignored by the XML deserializer. A useful workbook needs at least one worksheet; its name must be valid and unique under Excel/ClosedXML naming rules. Empty rows or worksheets are allowed, but empty styled/unstyled cells do not necessarily increase Excel's used range.

A neutral pack component can produce the same structure dynamically:

```vue
<script setup lang="ts">
import { inject } from 'vue'
type ReportData = { items: { name: string; amount: number }[] }
const { items } = inject<ReportData>('templateData')!
</script>

<template>
  <Workbook>
    <Worksheet name="Items">
      <Row v-for="(item, index) in items" :key="index">
        <Cell>{{ item.name }}</Cell>
        <Cell data-type="Number" style="number-format:0.00">{{ item.amount }}</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
```

Vue interpolation handles XML escaping. In hand-written XML, escape `&`, `<`, and attribute quotes. Do not use raw HTML output for unescaped input. DTDs and external XML entities are rejected. Unknown XML elements/attributes and unknown style keys are currently ignored rather than schema-validated, so spelling matters.

## Elements and attributes

| Element | Supported content and attributes |
| --- | --- |
| `Workbook` | Repeated `Worksheet`; `style` establishes workbook defaults. |
| `Worksheet` | Required `name`; repeated `Row`, optional `LastPageRow`, optional `PrintingArea`, repeated `NamedRange`; `style`, `fit-columns-to-page`, `freeze-pane-from-start`. |
| `Row` | Repeated `Cell`; `style`, `excel-row-ref`, `auto-filter`, `hide`, `freeze-pane`, `render-last-page-row`. |
| `Cell` | Text or a `FormulaR1C1` child; `style`, `data-type`, `colspan`, `rowspan`, `fit-column`, `fit-row`, `wrap-text`, `excel-row-ref`, `excel-column-ref`, `excel-named`, `image-src`, `image-width`, `image-height`. |
| `NamedRange` | `name` attribute and an R1C1 range as text, such as `R1C1:R5C2`. The name belongs to the worksheet. |
| `PrintingArea` | R1C1 range text; `orientation` (`Portrait` or `Landscape`), `paper-size` (`A4` or `A3`); optional `RowsToRepeatAtTop` child with a row range. |
| `LastPageRow` | A row-shaped footer inserted when the existing pagination heuristic detects a page boundary. It is not an unconditional final-summary row. |

Use `true` or `false` for boolean attributes. Defaults are false, except page-footer rendering starts enabled. Omitted `render-last-page-row` retains the preceding row's section setting. `PrintingArea` defaults to Portrait/A4 when present; enum spelling for these XML attributes must match the listed case.

Rows are laid out from row 1 downward. Cells fill columns from 1 onward. A spanning cell is placed at the first position where its entire rectangle is free of earlier merges; checking only its top-left cell is insufficient when an earlier rowspan occupies a later column. Use positive integer `colspan` and `rowspan`; a span greater than one merges the rectangle. Styles on a merged cell apply to the whole merged range. Cell widths change the worksheet column; cell heights change the row, so later settings can affect earlier cells in the same column/row.

Although the XML model recognizes some shared attributes on other nodes, tracking is implemented only for `excel-row-ref` on `Row`/`Cell`, `excel-column-ref` on `Cell`, and `excel-named` on `Cell`. Do not rely on these attributes on `Workbook` or `Worksheet`.

## Values and formulas

`data-type` is case-insensitive and defaults to `Text`. Empty values produce blank cells. Nonempty whitespace-only Text values remain text, and a leading ASCII apostrophe is preserved as content rather than consumed as Excel quote-prefix metadata.

| Type | Input | Behavior |
| --- | --- | --- |
| `Text` | Any escaped text | Writes text. It also applies the legacy HTML-decoding/newline conversion described below. |
| `Number` | Invariant numeric text, such as `1234.50` | Writes a numeric value; invalid numeric input remains text. Use a decimal point. |
| `Boolean` | `true` or `false` | Writes a boolean; invalid input remains text. |
| `DateOnly` | `yyyy-MM-dd` | Writes an Excel date; invalid input remains text. Apply `number-format` for its display. |

An unknown `data-type` falls back to text. Formatting changes presentation, not the submitted numeric value.

```xml
<Row>
  <Cell data-type="Number">12.5</Cell>
  <Cell data-type="Number">2</Cell>
  <Cell style="number-format:0.00">
    <FormulaR1C1>RC[-2]*RC[-1]</FormulaR1C1>
  </Cell>
</Row>
```

`FormulaR1C1` takes Excel R1C1 formula text without a leading `=`. A formula takes precedence over cell text; an image takes precedence over both. The service writes formulas and does not guarantee calculated or cached results in the response. Excel or another compatible spreadsheet application computes them when the workbook is opened/recalculated. The service is not a formula-evaluation API, and consumers must not treat missing cached values as computed zeroes.

Named row/column references substitute numbers into formulas and range declarations:

```xml
<Row excel-row-ref="firstItem">
  <Cell excel-column-ref="amount" data-type="Number" excel-named="FirstAmount">12.5</Cell>
</Row>
<Row>
  <Cell><FormulaR1C1>SUM(R{firstItem}C{amount}:R{firstItem}C{amount})</FormulaR1C1></Cell>
</Row>
<NamedRange name="Amounts">R{firstItem}C{amount}:R{firstItem}C{amount}</NamedRange>
```

Names are case-sensitive. Multiple names can be separated by spaces in a reference attribute. References must already have been encountered when a cell formula is rendered; `NamedRange` and print ranges are resolved after the worksheet rows. Unresolved placeholders remain in the expression. Whitespace inside braces and between an axis and its brace is accepted (`R { firstItem }C { amount }`); arbitrary whitespace between R/C address parts is retained and may be invalid Excel syntax.

Built-in numeric placeholders are `currentRow`, `currentColumn`, `pageNumber`, `firstPageRow`, and `lastPageRow`. The last two describe the renderer's pagination state; `lastPageRow` is assigned when a boundary is detected. Reference names persist across worksheets within one workbook for compatibility, but never across separate requests. Avoid reusing built-in names or depending on cross-sheet reference leakage.

`excel-named` names a single cell. `NamedRange`/`PrintingArea` conversion requires colon notation, even for a single cell: `R1C1:R1C1`. Row-only ranges use `R1:R3`. These declarations accept absolute numbered R1C1 ranges; relative `R[-1]` notation belongs in formula expressions, not range declarations.

## Styles

`style` is a semicolon-separated list of `key:value` declarations. Keys and enum values are case-insensitive. This is a small workbook styling language, not a browser CSS implementation: dimensions are plain numbers without `px`/`pt` suffixes, quoting is not removed, and there is no selector or stylesheet support. Workbook, worksheet, row, then cell styles establish progressively more specific formatting.

| Key | Supported values / meaning |
| --- | --- |
| `font-family` | Font family name, without CSS quotes; the container must have suitable font files for measurement. |
| `font-size` | Numeric point size. |
| `font-weight` | `normal`, `bold`. |
| `text-align` | `left`, `right`, `center`, `justify`. |
| `vertical-align` | `top`, `bottom`, `center`. |
| `text-direction` | `horizontal` (0°), `vertical` (90°). |
| `text-wrap` | `nowrap`, `wrap`. |
| `number-format` | Excel number-format text, such as `0.00` or `yyyy-mm-dd`; see delimiter limitation below. |
| `background-color` | HTML color accepted by ClosedXML, e.g. `#ffff00`. |
| `color` | Font color, e.g. `#000000`. |
| `border` | Numeric border thickness category applied to all four edges. |
| `border-top`, `border-bottom`, `border-left`, `border-right` | Override individual edge categories. |
| `border-color` | Color for all four borders. |
| `border-top-color`, `border-bottom-color`, `border-left-color`, `border-right-color` | Override individual edge colors. |
| `width` | Column width in Excel column-width units; implemented on cells. |
| `height` | Row height in points; implemented on rows and cells. |
| `break-after` | `always`; a row adds a horizontal break, a cell adds a vertical break after its column. |

Border numeric categories follow the existing renderer: values below 1 → none, `[1,2)` → thin, `[2,3)` → medium, `[3,4)` → thick, 4 or above → dash-dot. They are not pixel widths.

`fit-column="true"` uses ClosedXML's font-aware column measurement at that point in rendering. `fit-row="true"`, `wrap-text="true"`, and effective `text-wrap:wrap` schedule row-height estimation after rows are laid out. Wrapping inherited from workbook, worksheet, or row styles triggers the same fitting as a cell style; a more specific `text-wrap:nowrap` overrides inherited wrapping. The estimator uses character counts, column width, font size, and simple font-family factors; it is not Excel's layout engine. Custom production fonts can change column measurements. Row heights only grow during fitting.

## Printing, freezing, and images

```xml
<Worksheet name="Summary" fit-columns-to-page="true" freeze-pane-from-start="true">
  <Row freeze-pane="true"><Cell>Heading</Cell><Cell>Value</Cell></Row>
  <Row><Cell>Total</Cell><Cell data-type="Number">42</Cell></Row>
  <PrintingArea orientation="Landscape" paper-size="A4">R1C1:R2C2<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>
</Worksheet>
```

A print area writes a print range and selects page-break-preview in the serialized workbook. `fit-columns-to-page` requests one printed page across with no fixed page count down. `RowsToRepeatAtTop` takes an ascending row-only range. The first `Row freeze-pane="true"` determines the freeze boundary. With `freeze-pane-from-start="true"`, all rows from row 1 through that row freeze. Otherwise, if repeated print rows are configured, only their count is frozen and the view starts at their first row. Without repeated rows, freezing starts at row 1.

`auto-filter="true"` applies an autofilter to the explicit header-cell range after cell placement, including empty header cells and merged ranges inherited from preceding header rows. Rows with neither emitted cells nor intersecting merged ranges emit no filter. It does not select a complete multirow data table. Earlier versions emitted a whole-row reference such as `5:5`, which some spreadsheet readers reject; this interoperability defect is fixed. `hide="true"` hides the row and excludes its height from the pagination sum.

For embedded images, set `image-src` to a base64 data URI such as `data:image/png;base64,...` with actual valid image bytes. The image stretches from the top-left to the outer bottom-right edge of its entire cell/merged range and moves/sizes with the cells. Its end marker is normally the next row and column after the range, so even a single-cell image has positive extent; at Excel sheet limits, offsets in the last legal cell encode the outer edge. Placement is finalized after row fitting. The renderer does not fetch image URLs or read image files itself. Sources without a `base64,` marker produce no image; malformed base64/image data fails rendering. `image-width` and `image-height` are parsed as integers but intentionally unused in current layout. Prefer a merged range with explicit dimensions for image placement.

## Retained compatibility limitations

- Text is HTML-decoded after XML parsing. Escaped `<br/>`/`<br>` sequences are converted to newlines; literal entity-like strings can consequently decode twice. Use representative tests for text that must remain literal.
- Style parsing splits on every colon and semicolon. Values containing either delimiter, such as time formats or multisection Excel number formats, are not preserved reliably.
- Pagination uses heuristic height limits, evaluates boundaries before deferred fitted row heights, and inserts `LastPageRow` only at detected boundaries. It is not a print-engine preview or a guaranteed footer on the final page.
- Fit-to-page pagination uses a temporary worksheet named `__scale_probe__`; avoid that worksheet name. The temporary sheet does not reproduce all inherited workbook/worksheet styling.
- Named-range conversion needs a colon range. Reference scope spans worksheets. Image size attributes remain unused, as described above.
- `options.precisionAsDisplayed=true` changes the serialized Excel calculation setting (`fullPrecision=false`); it does not round every submitted numeric value in the service. Reopening/resaving with another library can drop this setting unless the consumer reapplies it.

The limitations above are part of the current workbook format. Coverage is described in [testing](testing.md). The included regression tests verify emitted workbook semantics; they do not establish exact pagination in every Excel version or guarantee support for every possible Excel formula.
