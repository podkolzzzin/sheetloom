<script setup>
import {inject} from 'vue';
const data=inject('templateData');
await Promise.resolve();
if(data.fail)throw new Error('private asynchronous failure '+data.secret);
</script>
<template><Row><Cell>complete</Cell></Row></template>
