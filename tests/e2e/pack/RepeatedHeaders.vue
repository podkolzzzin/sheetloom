<template>
  <Workbook><Worksheet name="Repeated">
    <Row style="height:200"><Cell>header</Cell></Row>
    <Row v-for="n in 4" style="height:150"><Cell>{{ n }}</Cell></Row>
    <LastPageRow><Cell>subtotal</Cell></LastPageRow>
    <PrintingArea orientation="Landscape">R1C1:R7C1<RowsToRepeatAtTop>R1:R1</RowsToRepeatAtTop></PrintingArea>
  </Worksheet></Workbook>
</template>
