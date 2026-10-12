# Create a template pack

[← README](../../README.md)

Create a `my-pack/` directory with a `manifest.json` file:

```json
{"apiVersion":1,"templates":{"items":"Items.vue"}}
```

Add `Items.vue` alongside it:

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

Mount `my-pack/` at `/templates:ro` instead of `examples/basic/` in the README command. Send `POST /v1/render/items` with `{"data":{"items":[{"name":"Paper","amount":12.5}]}}`. The result is an Excel workbook with one row on the `Items` sheet.

**Details.** The HTTP `data` wrapper is removed before the template receives `.body`. Validate template-specific fields in your code: the engine validates only the request envelope. Manifest paths are relative to the pack; imports and symlinks must stay inside it. Packs compile at startup, so restart the container after changes.
