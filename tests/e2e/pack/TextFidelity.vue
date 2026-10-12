<script setup>
import { inject } from 'vue';
const { values } = inject('templateData');
</script>
<template><Workbook><Worksheet name="Text">
  <Row v-for="value in values"><Cell data-type="Text">{{ value }}</Cell><Cell>row marker</Cell></Row>
</Worksheet></Workbook></template>
