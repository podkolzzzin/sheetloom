<script setup>
import { inject } from 'vue';
const data=inject('templateData');
</script>
<template>
  <Workbook><Worksheet name="Table">
    <Row><Cell>Id</Cell><Cell>Amount</Cell><Cell>Double</Cell></Row>
    <Row v-for="(item,index) in data.items" :key="index"><Cell>{{ item.id }}</Cell><Cell data-type="Number">{{ item.amount }}</Cell><Cell><FormulaR1C1>RC[-1]*2</FormulaR1C1></Cell></Row>
    <Row><Cell>End</Cell></Row>
  </Worksheet></Workbook>
</template>
