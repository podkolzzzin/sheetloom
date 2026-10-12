<script setup>
import { getTemplateData } from 'sheetloom';
import { dataUri } from '@/assets';
const { columns, rows } = getTemplateData();
const image = await dataUri(new URL('./assets/sample.png', import.meta.url));
</script>
<template><Workbook><Worksheet name="Image extent">
  <Row style="height:50"><Cell :colspan="columns" :rowspan="rows" :image-src="image" style="width:20"/></Row>
</Worksheet></Workbook></template>
