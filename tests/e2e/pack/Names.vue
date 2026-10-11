<template>
  <Workbook>
    <Worksheet name="Alpha">
      <Row excel-row-ref="start"><Cell excel-named="Anchor" excel-column-ref="amount" data-type="Number">5</Cell><Cell data-type="Number">7</Cell></Row>
      <Row><Cell><FormulaR1C1>SUM(R { start }C { amount }:R{start}C2)</FormulaR1C1></Cell></Row>
      <NamedRange name="InputRange">R1C1:R1C2</NamedRange>
    </Worksheet>
    <Worksheet name="Beta">
      <Row><Cell>label</Cell><Cell excel-named="Anchor" data-type="Number">11</Cell></Row>
      <Row><Cell><FormulaR1C1>'Alpha'!R1C1+R[-1]C[1]</FormulaR1C1></Cell></Row>
      <NamedRange name="InputRange">R1C2:R1C2</NamedRange>
    </Worksheet>
  </Workbook>
</template>
