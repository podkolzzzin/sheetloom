<script setup>
import { inject } from 'vue';
const { items } = inject('ssrRequest').body;
</script>
<template>
  <Workbook>
    <Worksheet name="Report" style="number-format:0.00">
      <Row style="font-weight:bold;background-color:#eaf0f6">
        <Cell style="width:24">Item</Cell>
        <Cell style="width:14">Amount</Cell>
      </Row>
      <Row v-for="(item, i) in items" :key="i">
        <Cell>{{ item.name }}</Cell>
        <Cell data-type="Number">{{ item.amount }}</Cell>
      </Row>
      <Row style="font-weight:bold">
        <Cell>Total</Cell>
        <Cell v-if="items.length"><FormulaR1C1>SUM(R2C2:R[-1]C2)</FormulaR1C1></Cell>
        <Cell v-else data-type="Number">0</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
