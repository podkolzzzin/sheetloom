<script setup>
import {getTemplateData} from 'sheetloom';
const data=getTemplateData();
</script>
<template>
  <Workbook><Worksheet name="Scale" fit-columns-to-page="true">
    <Row v-for="n in 4" style="height:300"><Cell :style="'width:'+data.width">{{ n }}</Cell><Cell :style="'width:'+data.width">body</Cell></Row>
    <LastPageRow><Cell>subtotal</Cell></LastPageRow>
    <PrintingArea orientation="Landscape">R1C1:R8C2</PrintingArea>
  </Worksheet></Workbook>
</template>
