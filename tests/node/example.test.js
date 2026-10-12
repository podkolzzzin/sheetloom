import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { compilePack } from '../../src/pack.js';
import { renderXml } from '../../src/render.js';

test('public invoice example handles documented, empty and invalid input', async t => {
  const cache = await mkdtemp(path.join(tmpdir(), 'invoice-example-'));
  t.after(() => rm(cache, { recursive: true, force: true }));
  const pack = await compilePack(path.resolve('examples/basic'), cache);
  t.after(pack.dispose);
  const render = data => renderXml(pack.bundle, 'invoice', data, 10000);

  await t.test('README request produces numeric items and a bounded total formula', async () => {
    const { data } = JSON.parse(await readFile('examples/basic/request.json', 'utf8'));
    const xml = await render(data);
    assert.match(xml, /<Worksheet name="Invoice">/);
    assert.match(xml, /data-type="Number"[^>]*>42\.5<\/Cell>/);
    assert.match(xml, /data-type="Number"[^>]*>7\.5<\/Cell>/);
    assert.match(xml, /<FormulaR1C1>SUM\(R2C2:R\[-1\]C2\)<\/FormulaR1C1>/);
  });
  await t.test('an empty invoice has numeric zero and no circular formula', async () => {
    const xml = await render({ customer: 'Empty', items: [] });
    assert.doesNotMatch(xml, /<FormulaR1C1>/);
    assert.match(xml, /data-type="Number"[^>]*>0<\/Cell>/);
  });
  await t.test('customer text is escaped for XML', async () => {
    const xml = await render({ customer: 'A & <B>', items: [] });
    assert.match(xml, /A &amp; &lt;B&gt;/);
  });
  await t.test('invalid shapes and numeric strings fail safely', async () => {
    for (const data of [null, {}, { customer: 'X', items: null },
      { customer: 'X', items: [null] }, { customer: 'X', items: [{ name: 'X', amount: '12.5' }] },
      { customer: 'X', items: [{ name: 'X', amount: Infinity }] }]) {
      await assert.rejects(render(data), error => error.status === 422 && error.code === 'template_failed');
    }
  });
});
