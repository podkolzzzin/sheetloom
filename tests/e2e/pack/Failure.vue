<script setup>
import { inject } from 'vue';
const data=inject('ssrRequest').body;
if(data.fail) throw new Error('secret from request: '+data.secret);
</script>
<template><Workbook><Worksheet name="Recovery"><Row><Cell>recovered</Cell></Row></Worksheet></Workbook></template>
