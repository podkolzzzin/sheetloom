<script setup>
import { inject } from 'vue';
const { items } = inject('ssrRequest').body;
</script>
<template>
  <Workbook>
    <Worksheet name="Report">
      <Row v-for="(item, i) in items" :key="i">
        <Cell>{{ item.name }}</Cell>
        <Cell data-type="Number">{{ item.amount }}</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
