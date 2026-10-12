<script setup>
import { getTemplateData } from 'sheetloom';
const data = getTemplateData();
</script>
<template>
  <Workbook><Worksheet :name="data.sheet || 'Values'">
    <Row><Cell>{{ data.text }}</Cell><Cell>{{ data.identifier }}</Cell><Cell>{{ data.formulaLike }}</Cell></Row>
    <Row><Cell data-type="Number" style="number-format:0.00">{{ data.amount }}</Cell><Cell data-type="Boolean">{{ data.flag }}</Cell><Cell data-type="DateOnly" style="number-format:yyyy-mm-dd">{{ data.date }}</Cell></Row>
    <Row><Cell data-type="Number">0</Cell><Cell data-type="Number">-12.5</Cell><Cell data-type="Number">1e3</Cell><Cell data-type="Boolean">false</Cell></Row>
    <Row><Cell data-type="Number"></Cell><Cell data-type="DateOnly"></Cell><Cell data-type="Boolean"></Cell><Cell>anchor</Cell></Row>
    <Row><Cell data-type="Number">invalid number</Cell><Cell data-type="DateOnly">2024-02-30</Cell><Cell data-type="Boolean">perhaps</Cell></Row>
  </Worksheet></Workbook>
</template>
