<script setup lang="ts">
import moment from 'moment';
import { inject } from 'vue';
import { next } from '@/state';
const data = inject<any>('templateData');
const counter = next();
</script>
<template>
  <Workbook><Worksheet name="Locale">
    <Row><Cell>{{ moment('2024-01-15').format('MMMM') }}</Cell><Cell>{{ data.token }}</Cell><Cell data-type="Number">{{ counter }}</Cell></Row>
  </Worksheet></Workbook>
</template>
