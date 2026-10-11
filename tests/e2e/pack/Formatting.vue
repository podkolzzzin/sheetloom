<template>
  <Workbook style="font-family:DejaVu Sans;font-size:10;color:#112233">
    <Worksheet name="Styles" style="font-size:12">
      <Row style="font-weight:bold;height:35">
        <Cell colspan="2" rowspan="2" style="font-size:16;text-align:center;vertical-align:top;text-wrap:wrap;background-color:#ffff00;border:1;border-top:2;border-right:3;border-color:#0000ff;border-top-color:#00ff00">Merged heading</Cell>
        <Cell style="width:30;number-format:0.000" data-type="Number">1.2345</Cell>
      </Row>
      <Row><Cell>after merge</Cell></Row>
      <Row hide="true"><Cell>hidden row</Cell></Row>
      <Row style="break-after:always"><Cell style="break-after:always;text-direction:vertical">vertical</Cell><Cell>other</Cell></Row>
      <Row auto-filter="true"><Cell>Key</Cell><Cell>Value</Cell></Row>
    </Worksheet>
  </Workbook>
</template>
