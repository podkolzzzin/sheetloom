<script setup>
import { readFileSync } from 'node:fs';
import { inject } from 'vue';
const { backtick } = inject('ssrRequest').body;
const value = readFileSync(backtick
  ? new URL(`./assets/literal.txt`, import.meta.url)
  : new URL('./assets/literal.txt', import.meta.url), 'utf8');
</script>
<template><Workbook><Worksheet name="Asset"><Row><Cell>{{ value }}</Cell></Row></Worksheet></Workbook></template>
