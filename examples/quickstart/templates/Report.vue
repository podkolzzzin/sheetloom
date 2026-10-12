<script setup lang="ts">
import { getTemplateData } from 'sheetloom';
type ReportData = { items: { name: string; amount: number }[] };
const { items } = getTemplateData<ReportData>();
</script>
<template>
  <Workbook>
    <Worksheet name="Report" style="number-format:0.00">
      <Row style="font-weight:bold;background-color:#eaf0f6">
        <Cell style="width:24">Item</Cell>
        <Cell style="width:14">Amount</Cell>
      </Row>
      <Row v-for="(item, i) in items" :key="i"
        :excel-row-ref="i === 0 ? 'firstItem lastItem' : 'lastItem'">
        <Cell>{{ item.name }}</Cell>
        <Cell excel-column-ref="amount" data-type="Number">{{ item.amount }}</Cell>
      </Row>
      <Row style="font-weight:bold">
        <Cell>Total</Cell>
        <Cell v-if="items.length">
          <FormulaR1C1>SUM(R{firstItem}C{amount}:R{lastItem}C{amount})</FormulaR1C1>
        </Cell>
        <Cell v-else data-type="Number">0</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
