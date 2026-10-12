<script setup>
import { inject } from 'vue';
const { level } = inject('templateData');
</script>
<template>
  <Workbook :style="'font-size:14;' + (level === 'workbook' ? 'text-wrap:wrap' : '')">
    <Worksheet name="Wrap" :style="level === 'worksheet' ? 'text-wrap:wrap' : ''">
      <Row :style="['row', 'nowrap'].includes(level) ? 'text-wrap:wrap' : ''">
        <Cell :style="'width:10;' + (level === 'cell' ? 'text-wrap:wrap' : level === 'nowrap' ? 'text-wrap:nowrap' : '')">The quick brown fox jumps over the lazy dog.</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
