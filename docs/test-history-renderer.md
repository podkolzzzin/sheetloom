# Renderer regressions and test evidence

The renderer tests use synthetic workbooks to verify cell values, formulas, styles, printing and request isolation. Every fixture and expected result is maintained in this repository.

Run the suite with:

```sh
dotnet restore dotnet/Sheetloom.sln --locked-mode
dotnet test dotnet/Sheetloom.sln --no-restore
```

The suite inspects rendered workbook values/geometry and persisted ZIP/XML parts. Tests inspecting formulas distinguish formula text from cached results: the service does not promise a calculation engine. A few tests reopen workbooks with ClosedXML to check formula references against known cell values; this is test-side evaluation, not a claim that the HTTP response contains calculated caches. The separate Python end-to-end suite provides an independent workbook-reader check.

## Behaviors covered

| Behavior | Regression coverage |
| --- | --- |
| Named cells moved from global names to worksheet-local definitions. | Two sheets reuse `Amount` without conflicts; each resolves its own cell; serialized names have distinct local sheet IDs and quote an apostrophe in a sheet name correctly. |
| Formula substitution changed to recognize whitespace around named placeholders. | Compact, spaced, and newline/tab reference forms produce the same absolute cell formula; similar names (`amount`/`amountTotal`) do not collide. |
| Page totals must not split a vertically merged group. | A subtotal boundary crosses a two-row merge: the footer follows the entire group, refers to all its values, and preserves the following body row. |
| Repeated headers and section-controlled footer rendering. | Successive footer formulas address physical body rows after earlier footers, exclude earlier totals, and carry the right page number. Existing tests also cover disabling a footer section. |
| Pagination gained a width probe and reserved repeated-header height on later pages. | A late column width declaration yields the same page boundaries as an early declaration; repeated headers consume later-page height; hidden headers do not. Exact fixture boundaries detect omission of header reservation. |
| Row fitting was deferred until final widths were known; font-family factors were added. | Early and late width declarations yield equal fitted heights; merged cells use all columns; an explicit minimum row height is preserved; inherited proportional/monospace fonts and explicit line breaks change height appropriately. |
| Scrolled header freezing, freeze-from-start override, and the eventual page-break-preview setting. | Inspect emitted `sheetView`/`pane` XML for both freeze variants, repeated rows resolved through names, first-marker behavior, and print titles without a freeze marker. Reject descending, column-containing, and incomplete repeated-row ranges. |
| Embedded images and merged-cell image placement. | Embedded image bytes are identical to the input; the drawing's two-cell anchor covers the intended merge; image precedence suppresses text/formulas. |
| Precision-as-displayed must be written after ClosedXML serialization. | Precision changes only the calculation flag, preserving other workbook calculation attributes, sheet formulas/merges, styles, and shared strings. Both option values are also checked in actual HTTP response bytes. |
| Invalid typed input should remain text instead of failing the report. | Existing typed-value tests are extended with nonfinite/overflowed numbers and dates under non-Gregorian host cultures; these exposed two defects described below. |

The first 45 tests remain in place. The additional historical, edge-case, and lifecycle files cover interactions that single-feature smoke tests did not: inserted rows versus formulas, inherited fonts versus deferred dimensions, merges versus pagination, shared names versus worksheet scope, and cancellation versus rendering capacity.

## Defects found by the expanded suite

### ISO dates depended on the host calendar

Before the fix, `DateOnly` parsing used the current culture. The input `2024-02-29` became text under `th-TH`/`ar-SA`; under `fa-IR`, it became **2645-05-19**. Three tests failed before the change. Parsing now explicitly uses the invariant Gregorian culture. ISO date semantics no longer depend on the process locale; existing Gregorian inputs retain their values.

### Nonfinite numbers failed the whole workbook

`double.TryParse` accepts `NaN`, positive/negative infinity, and sufficiently large exponents, while Excel numeric cells cannot represent them. Four tests reproduced ClosedXML exceptions for `NaN`, `Infinity`, `-Infinity`, and `1e9999`. The renderer now checks `double.IsFinite`; these inputs follow the existing invalid-number behavior and remain text. Finite numeric values are unchanged.

### Whole-row autofilter references were not interoperable

The independent Python/openpyxl end-to-end test could not open a generated workbook because the old implementation emitted `<autoFilter ref="5:5"/>`. The renderer set an autofilter on the entire row before it had placed header cells.

It now applies an explicit cell range after placement, such as `A5:B5`, including blank headers and merged spans. Both ordinary rows and inserted footer rows use this behavior. XML-level regression cases verify ordinary, blank, merged, footer, and empty-row cases.

A subsequent independent header-boundary audit found that the initial correction omitted trailing columns inherited through rowspans from a preceding header row. Neutral tests first reproduced a shortened `A2:B2` instead of `A2:C2`, and a missing filter for a header row containing only inherited merged cells. The bound now includes every merged range intersecting the header row. A third test verifies that wider merges ending before the header do not widen its filter. Rows with neither emitted cells nor intersecting merges emit no filter. This intentionally corrects the old serialized autofilter shape; it is a compatibility change required for readers that reject whole-row filter references. It does not expand the filter to the complete table body.

## Six independently reported black-box regressions

A separate audit used only public documentation, mounted neutral template packs, HTTP responses, and openpyxl/ZIP inspection. All six findings were reproduced before implementation changes. Permanent focused tests cover the underlying mechanisms; the independent end-to-end cases remain separate.

| Finding | Correction and focused coverage |
| --- | --- |
| A colspan intersected a prior rowspan because its anchor was free. | Placement now checks the whole proposed rectangle before tracking references or merging. The test verifies nonintersecting merges, preserved values, the subsequent cell, and relocated formula/named-cell addresses. |
| Inherited `text-wrap:wrap` did not trigger fitted height. | Fitting reads the effective cell wrapping style after inheritance and overrides. Workbook/worksheet/row forms match explicit cell wrapping; explicit `nowrap` remains effective. |
| Single-cell images had identical start/end markers; one-dimensional merges also had a zero extent. | Images anchor to the outer edge of the whole rectangle after final row fitting. Tests cover 1×1, 1×2, 2×1, and 2×2 geometry. Last-column, last-row, and bottom-right-sheet-corner tests verify legal markers with positive offsets rather than constructing an out-of-bounds cell. The existing merged-image assertion was updated to the corrected exclusive end. |
| Static backtick `new URL` assets failed while equivalent quoted strings worked. | The AST recognizes untagged template literals with no expressions and uses their cooked value. Vue and TypeScript helper tests render mounted assets; an extraction control covers escapes and leaves dynamic/tagged forms alone. |
| Whitespace-only nonempty Text values disappeared during XML deserialization. | A delegating XML reader promotes whitespace directly inside `Cell` to text for the serializer. Outside indentation, DTD prohibition, and document-size enforcement remain governed by the original safe reader. Tests assert both the parsed model and persisted text for spaces, tabs, and line feeds. |
| A leading ASCII apostrophe was consumed by ClosedXML as quote-prefix metadata. | Such text uses a literal rich-text run after effective styles are applied. Tests preserve ordinary names, formula-looking text, repeated/single apostrophes, and the full 32,767-character Excel cell limit with inherited family, bold, size, and color. No process-global ClosedXML conversion setting is changed. |

These are deliberate compatibility corrections. Image drawings now include the final row/column of their rectangle; formerly overlapping merges may move right; inherited wrapping may increase row heights; formerly lost whitespace/apostrophes now remain data. Formula text, named-cell addresses, and filtering follow the resulting placement. Other documented formatting/parser limitations remain unchanged.

Before fixes, all 18 initial focused .NET cases and all three new Node asset cases failed. After fixes, the complete local suites passed **123 .NET cases** and **67 Node cases**. The .NET count includes four additional style/length and sheet-edge checks. The independent Docker suite verifies these behaviors through the HTTP API with a separate XLSX reader.

## Nonobvious input and lifecycle cases

- Formula-looking text (`=1+1`, `@SUM(A1)`) and leading-zero identifiers stay literal text.
- Trailing extra XML roots or garbage are rejected; empty sheets/rows do not shift or drop subsequent data.
- Duplicate worksheet names fail, and the same renderer can then serve a valid request.
- HTTP/file image sources are not fetched; corrupt base64 and invalid image bytes fail without poisoning later requests.
- Concurrent requests place the same reference name in different columns and retain the right formula address/value per request.
- Unknown-length/chunked request bodies cannot bypass the byte limit; a subsequent valid request still succeeds.
- Cancellation during upload and an interrupted body stream both release the rendering slot before invoking workbook construction.
- XML/DTD, duplicate-sheet, and invalid-style failures return safe errors and release capacity even after repeated failures.

## Tests were checked against deliberate regressions

Five isolated temporary-copy experiments reversed a representative behavior, then ran its targeted test. All five produced assertion/test failures, rather than compilation failures:

| Deliberate regression | Test that detected it |
| --- | --- |
| Stop accepting placeholder whitespace | `FormulaWhitespaceAndSimilarNamesResolveExactReferencedCell` |
| Stop reserving repeated-header height | `RepeatedHeaderHeightIsReservedOnLaterPages` |
| Fit rows immediately, before later width declarations | `DeferredFitUsesFinalColumnWidthInsteadOfFirstRowWidth` |
| Define cell names at workbook scope | `IdenticalDefinedNamesInDifferentSheetsStayLocalAndQuoteSheetNames` |
| Serialize `fullPrecision=true` despite the requested option | `PrecisionOptionDoesNotDropFormulasMergesStylesOrOtherCalculationAttributes` |

These were targeted mutation checks, not a complete mutation-testing campaign or exhaustive checks of every earlier engine revision. They verify that the new assertions detect their intended failure mechanisms. No deliberate mutation was applied to the working repository.

## Remaining limits

Pagination and wrapped-height calculations remain heuristics. A passing workbook-structure test does not establish exact printed pagination in every Excel version, installed font set, or printer configuration. Tests intentionally retain the documented formula reference scope, range notation, text decoding, CSS delimiter behavior, image sizing behavior, and temporary probe-name limitation unless a documented defect fix changes them. Memory/performance benchmarking and exhaustive Excel formula evaluation are outside this suite.
