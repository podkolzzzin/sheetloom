# Як додати підсумок без помилки на порожньому списку

[← README](../../README.md) · Нехай `items` — масив із поля `data.items`. Фрагмент усередині `<Worksheet>`:

```vue
<Row><Cell>Назва</Cell><Cell>Сума</Cell></Row>
<Row v-for="(item, i) in items" :key="i"
  :excel-row-ref="i === 0 ? 'firstItem lastItem' : 'lastItem'">
  <Cell>{{ item.name }}</Cell>
  <Cell excel-column-ref="amount" data-type="Number">{{ item.amount }}</Cell>
</Row>
<Row>
  <Cell>Разом</Cell>
  <Cell v-if="items.length" style="number-format:0.00">
    <FormulaR1C1>SUM(R{firstItem}C{amount}:R{lastItem}C{amount})</FormulaR1C1>
  </Cell>
  <Cell v-else data-type="Number">0</Cell>
</Row>
```

**Результат:** формула охоплює лише позиції; порожній список дає число `0` без циклічного посилання.

`excel-row-ref` запам'ятовує номер рядка, `excel-column-ref` — номер колонки. Кілька назв можна розділити пробілом; повторне використання назви оновлює її значення. Формула бачить тільки вже пройдені клітинки.

**Нюанс.** `{amount}` підставляє C#-рендерер, а `{{ item.amount }}` — Vue. Використовуй англійські функції та R1C1 без початкового `=`. Рушій записує формулу, але не гарантує її обчислений кеш: Excel перерахує її після відкриття. Для готового числового результату передавай обчислену суму з backend.
