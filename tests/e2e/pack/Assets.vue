<script setup lang="ts">
import { dataUri } from '@/assets';
const image = await dataUri(new URL('./assets/sample.png', import.meta.url));
const example = "new URL('./this-is-text-not-a-file.png', import.meta.url)";
</script>
<template>
  <Workbook><Worksheet name="Image">
    <Row style="height:40"><Cell colspan="2" rowspan="2" :image-src="image" style="width:20"/></Row>
    <Row><Cell>beside image</Cell></Row>
    <Row><Cell>{{ example }}</Cell></Row>
  </Worksheet></Workbook>
</template>
