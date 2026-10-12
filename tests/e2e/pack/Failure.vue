<script setup>
import { getTemplateData } from 'sheetloom';
const data=getTemplateData();
if(data.fail) throw new Error('secret from request: '+data.secret);
</script>
<template><Workbook><Worksheet name="Recovery"><Row><Cell>recovered</Cell></Row></Worksheet></Workbook></template>
