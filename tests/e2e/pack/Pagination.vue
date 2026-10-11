<template>
  <Workbook><Worksheet name="Pages">
    <Row style="height:500"><Cell rowspan="3">Group</Cell><Cell data-type="Number">1</Cell></Row>
    <Row style="height:500"><Cell data-type="Number">2</Cell></Row>
    <Row style="height:500"><Cell data-type="Number">3</Cell></Row>
    <LastPageRow><Cell>subtotal</Cell><Cell><FormulaR1C1>SUM(R{firstPageRow}C2:R{lastPageRow}C2)</FormulaR1C1></Cell></LastPageRow>
  </Worksheet></Workbook>
</template>
