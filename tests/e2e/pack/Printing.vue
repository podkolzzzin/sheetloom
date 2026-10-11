<script setup>
import { inject } from 'vue';
const data=inject('ssrRequest').body;
</script>
<template>
  <Workbook><Worksheet name="Print" fit-columns-to-page="true" :freeze-pane-from-start="data.fromStart">
    <Row><Cell>Title</Cell></Row>
    <Row><Cell>Header 1</Cell></Row>
    <Row freeze-pane="true"><Cell>Header 2</Cell></Row>
    <Row><Cell>Data</Cell></Row>
    <PrintingArea :orientation="data.orientation" :paper-size="data.paper">R1C1:R4C1<RowsToRepeatAtTop>R2:R3</RowsToRepeatAtTop></PrintingArea>
  </Worksheet></Workbook>
</template>
