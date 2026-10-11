# Як налаштувати друк і закріплення

[← README](../../README.md) · Встав цей аркуш у `<Workbook>`:

```vue
<Worksheet name="Items" fit-columns-to-page="true">
  <Row><Cell colspan="2">Перелік матеріалів</Cell></Row>
  <Row excel-row-ref="header" freeze-pane="true" auto-filter="true">
    <Cell>Назва</Cell><Cell>Кількість</Cell>
  </Row>
  <Row excel-row-ref="lastRow">
    <Cell>Папір</Cell><Cell data-type="Number">10</Cell>
  </Row>
  <PrintingArea paper-size="A4" orientation="Landscape">
    R1C1:R{lastRow}C2
    <RowsToRepeatAtTop>R{header}:R{header}</RowsToRepeatAtTop>
  </PrintingArea>
</Worksheet>
```

**Результат:** дві колонки друкуються на A4 горизонтально, по ширині однієї сторінки. Табличний заголовок повторюється на папері й закріплюється на екрані з початковою прокруткою до нього.

- `PrintingArea` задає друкований діапазон; колонки поза ним залишаються в книзі. Межі пишуться через двокрапку навіть для однієї клітинки.
- `RowsToRepeatAtTop` сам по собі впливає лише на друк. Для екранного закріплення потрібен `freeze-pane="true"` на останньому рядку заголовка.
- Щоб закріпити також титульні рядки вище, додай `freeze-pane-from-start="true"` на `Worksheet`.

Рендерер зберігає область друку з початковим режимом перегляду розривів сторінок. Для перевірки закріплення можна перейти у звичайне подання Excel. Висота рядків залежить від шрифтів; перевіряй друк у цільовій програмі, особливо після перенесення довгого тексту.
