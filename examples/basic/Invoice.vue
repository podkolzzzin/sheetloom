<script setup>
import { inject } from 'vue';
const data = inject('ssrRequest').body;
if (!data || typeof data.customer !== 'string' || !Array.isArray(data.items) ||
    !data.items.every(item => item && typeof item.name === 'string' &&
      typeof item.amount === 'number' && Number.isFinite(item.amount))) {
  throw new Error('Expected a customer and items with a name and finite numeric amount');
}
</script>

<template>
  <Workbook style="font-family:DejaVu Sans;font-size:11;">
    <Worksheet name="Invoice">
      <Row><Cell colspan="2" style="font-weight:bold;">{{ data.customer }}</Cell></Row>
      <Row v-for="(item, i) in data.items" :key="i"><Cell>{{ item.name }}</Cell><Cell data-type="Number" style="number-format:0.00;">{{ item.amount }}</Cell></Row>
      <Row>
        <Cell>Total</Cell>
        <Cell v-if="data.items.length" style="number-format:0.00;"><FormulaR1C1>SUM(R2C2:R[-1]C2)</FormulaR1C1></Cell>
        <Cell v-else data-type="Number" style="number-format:0.00;">0</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
