<script setup>
import {getTemplateData} from 'sheetloom';
const data=getTemplateData();
await Promise.resolve();
if(data.fail)throw new Error('private asynchronous failure '+data.secret);
</script>
<template><Row><Cell>complete</Cell></Row></template>
