<template>
  <Workbook>
    <Worksheet name="Inherited">
      <Row><Cell rowspan="2">Left</Cell><Cell>Group</Cell><Cell rowspan="2">Right</Cell></Row>
      <Row auto-filter="true"><Cell>Middle</Cell></Row>
      <Row><Cell>one</Cell><Cell>two</Cell><Cell>three</Cell></Row>
    </Worksheet>
    <Worksheet name="Fully inherited">
      <Row><Cell colspan="3" rowspan="2">All headings</Cell></Row>
      <Row auto-filter="true" />
      <Row><Cell>one</Cell><Cell>two</Cell><Cell>three</Cell></Row>
    </Worksheet>
  </Workbook>
</template>
