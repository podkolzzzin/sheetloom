# Add totals that handle empty lists

[← README](../../README.md) · Assume `items` comes from `data.items`. Place this fragment inside `<Worksheet>`:

```vue
<Row><Cell>Item</Cell><Cell>Amount</Cell></Row>
<Row v-for="(item, i) in items" :key="i"
  :excel-row-ref="i === 0 ? 'firstItem lastItem' : 'lastItem'">
  <Cell>{{ item.name }}</Cell>
  <Cell excel-column-ref="amount" data-type="Number">{{ item.amount }}</Cell>
</Row>
<Row>
  <Cell>Total</Cell>
  <Cell v-if="items.length" style="number-format:0.00">
    <FormulaR1C1>SUM(R{firstItem}C{amount}:R{lastItem}C{amount})</FormulaR1C1>
  </Cell>
  <Cell v-else data-type="Number">0</Cell>
</Row>
```

**Result:** the formula covers only item rows. An empty list produces numeric `0` without a circular reference.

`excel-row-ref` records a row number; `excel-column-ref` records a column number. Separate multiple names with spaces. Reusing a name updates its value. Formulas can reference only cells already visited by the renderer.

**Details.** The C# renderer replaces `{amount}`; Vue replaces `{{ item.amount }}`. Use English function names and R1C1 notation without a leading `=`. The engine writes formulas but does not guarantee cached results: Excel recalculates them when opened. If you need an immediately available numeric result, calculate it in your backend and pass it as data.
