<script setup>
import { inject } from 'vue';
const data = inject('ssrRequest').body;
</script>
<template><Workbook><Worksheet name="Merge placement">
  <Row><Cell>first</Cell><Cell rowspan="2">vertical</Cell></Row>
  <Row><Cell :colspan="data.wide ? 2 : 1">horizontal</Cell><Cell>last</Cell></Row>
</Worksheet></Workbook></template>
