import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { compilePack } from '../../src/pack.js';
import { renderXml } from '../../src/render.js';
import { makePack, compile } from './helpers/pack.js';

test('templateData reaches nested components unchanged and keeps old packs compatible', async t => {
  const fixture = await makePack(t, {
    'Report.vue': `<script setup>
      import DataRow from './DataRow.vue';
      </script><template><Workbook><Worksheet name="Data"><DataRow /></Worksheet></Workbook></template>`,
    'DataRow.vue': `<script setup lang="ts">
      import { inject } from 'vue';
      const data = inject<unknown>('templateData');
      const legacy = inject<{ body: unknown }>('ssrRequest')!;
      if (data === undefined || legacy.body !== data) throw new Error('Injection mismatch');
      const encoded = Buffer.from(JSON.stringify(data)).toString('base64');
      </script><template><Row><Cell>{{ encoded }}</Cell></Row></template>`,
  });
  const pack = await compile(t, fixture);
  const values = [null, false, 0, '', 'text', [1, 'two'], { body: 'actual data', items: [] }];
  for (const data of values) {
    const xml = await renderXml(pack.bundle, 'report', data, 10000);
    const expected = Buffer.from(JSON.stringify(data)).toString('base64');
    assert.ok(xml.includes(`<Cell>${expected}</Cell>`), `Payload changed: ${JSON.stringify(data)}`);
  }
});

test('typed quickstart renders named formula refs and handles one or zero items', async t => {
  const cache = await mkdtemp(path.join(tmpdir(), 'sheetloom-typed-example-'));
  t.after(() => rm(cache, { recursive: true, force: true }));
  const pack = await compilePack(path.resolve('examples/quickstart/templates'), cache);
  t.after(pack.dispose);

  for (const items of [
    [{ name: 'Consulting', amount: 42.5 }, { name: 'Materials', amount: 7.5 }],
    [{ name: 'Single', amount: 12.5 }],
    [],
  ]) {
    const xml = await renderXml(pack.bundle, 'report', { items }, 10000);
    assert.match(xml, /number-format:0\.00/);
    if (items.length) {
      assert.match(xml, /excel-row-ref="firstItem lastItem"/);
      assert.match(xml, /excel-column-ref="amount"/);
      assert.match(xml, /<FormulaR1C1>SUM\(R\{firstItem\}C\{amount\}:R\{lastItem\}C\{amount\}\)<\/FormulaR1C1>/);
    } else {
      assert.doesNotMatch(xml, /<FormulaR1C1>/);
      assert.match(xml, /data-type="Number"[^>]*>0<\/Cell>/);
    }
  }
});
