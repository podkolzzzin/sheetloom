# Як додати власний шаблон

[← README](../../README.md)

Створи папку `my-pack/` із файлом `manifest.json`:

```json
{"apiVersion":1,"templates":{"items":"Items.vue"}}
```

Поруч створи `Items.vue`:

```vue
<script setup>
import { inject } from 'vue';
const { items } = inject('ssrRequest').body;
</script>
<template>
  <Workbook>
    <Worksheet name="Items">
      <Row v-for="(item, i) in items" :key="i">
        <Cell>{{ item.name }}</Cell>
        <Cell data-type="Number">{{ item.amount }}</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
```

Монтуй `my-pack/` у `/templates:ro` замість `examples/basic/` у команді з README. Надішли `POST /v1/render/items` із тілом `{"data":{"items":[{"name":"Папір","amount":12.5}]}}`. Результат — Excel з одним рядком на аркуші `Items`.

**Нюанси.** HTTP-обгортка `data` не потрапляє в `.body` повторно. Поля самого шаблону перевіряй у його коді: рушій перевіряє лише загальну структуру запиту. Шляхи в manifest відносні до пакета; імпорти й симлінки не можуть виходити за його межі. Пакет компілюється при старті — після змін перезапусти контейнер.
