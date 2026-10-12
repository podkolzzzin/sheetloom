# Configure printing and frozen headers

[← README](../../README.md) · Place this worksheet inside `<Workbook>`:

```vue
<Worksheet name="Items" fit-columns-to-page="true">
  <Row><Cell colspan="2">Materials list</Cell></Row>
  <Row excel-row-ref="header" freeze-pane="true" auto-filter="true">
    <Cell>Item</Cell><Cell>Quantity</Cell>
  </Row>
  <Row excel-row-ref="lastRow">
    <Cell>Paper</Cell><Cell data-type="Number">10</Cell>
  </Row>
  <PrintingArea paper-size="A4" orientation="Landscape">
    R1C1:R{lastRow}C2
    <RowsToRepeatAtTop>R{header}:R{header}</RowsToRepeatAtTop>
  </PrintingArea>
</Worksheet>
```

**Result:** both columns fit across one landscape A4 page. The table header repeats on printed pages and stays frozen on screen, with the initial view scrolled to it.

- `PrintingArea` defines the printed range; columns outside it remain in the workbook. Use a colon between range endpoints, even for one cell.
- `RowsToRepeatAtTop` alone affects printing. Add `freeze-pane="true"` to the last header row to freeze it on screen.
- To freeze the title rows above it as well, add `freeze-pane-from-start="true"` to `Worksheet`.

The renderer saves print areas with an initial page-break-preview view. Switch to Excel's Normal view to inspect frozen rows. Row height depends on fonts; check printing in your target application, especially when wrapping long text.
