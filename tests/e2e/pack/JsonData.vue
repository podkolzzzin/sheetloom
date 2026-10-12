<script setup>
import { getTemplateData } from 'sheetloom';
const data = getTemplateData();
const encoded = JSON.stringify(data);
const cleanPrototype = !Object.hasOwn(Object.prototype, 'sheetloomPolluted');
</script>
<template><Workbook><Worksheet name="JSON">
  <Row><Cell>{{ encoded }}</Cell><Cell data-type="Boolean">{{ cleanPrototype }}</Cell></Row>
</Worksheet></Workbook></template>
