<script setup>
import { inject } from 'vue';
import { dataUri } from '@/assets';
const { columns, rows } = inject('ssrRequest').body;
const image = await dataUri(new URL('./assets/sample.png', import.meta.url));
</script>
<template><Workbook><Worksheet name="Image extent">
  <Row style="height:50"><Cell :colspan="columns" :rowspan="rows" :image-src="image" style="width:20"/></Row>
</Worksheet></Workbook></template>
