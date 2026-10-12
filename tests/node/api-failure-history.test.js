import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, access, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { setup } from './helpers/server.js';
const post = (app, data) => app.inject({ method: 'POST', url: '/render/invoice', payload: { data } });

test('multi-byte XML limit is measured in UTF-8 bytes and releases capacity', async t => {
  const app = await setup(t, { concurrency: 1 }, `export function render(id,data){return data==='huge'?'ї'.repeat(4*1024*1024+1):'<Workbook/>'}`);
  const rejected = await post(app, 'huge');
  assert.equal(rejected.statusCode, 413);
  assert.equal(rejected.json().error.code, 'output_too_large');
  assert.equal((await post(app, 'small')).statusCode, 200);
});
test('exact XML byte limit is accepted', async t => {
  const app = await setup(t, {}, `export function render(){return 'x'.repeat(8*1024*1024)}`);
  const response = await post(app, null);
  assert.equal(response.statusCode, 200);
  assert.equal(Buffer.byteLength(response.body), 8*1024*1024);
});
test('upstream disconnect terminates a hung worker before its deadline and releases capacity', async t => {
  const directory = await mkdtemp(path.join(tmpdir(), 'abort-worker-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const marker = path.join(directory, 'started');
  const source = `import {writeFileSync} from 'node:fs';export function render(id,data){if(data==='hang'){writeFileSync(${JSON.stringify(marker)},'started');while(true){}}return '<Workbook/>'}`;
  const app = await setup(t, { concurrency: 1, timeoutMs: 30000 }, source);
  await app.listen({ host: '127.0.0.1', port: 0 });
  const cancellation = new AbortController();
  const pending = fetch(`http://127.0.0.1:${app.server.address().port}/render/invoice`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: '{"data":"hang"}', signal: cancellation.signal,
  });
  const aborted = assert.rejects(pending, { name: 'AbortError' });
  let started = false;
  for (let i = 0; i < 200; i++) {
    try { await access(marker); started = true; break; } catch { }
    await new Promise(resolve => setTimeout(resolve, 20));
  }
  assert.ok(started, 'worker should begin before cancellation');
  assert.equal((await post(app, 'probe')).statusCode, 503);
  cancellation.abort(); await aborted;
  let recovered = false;
  for (let i = 0; i < 100; i++) {
    if ((await post(app, 'good')).statusCode === 200) { recovered = true; break; }
    await new Promise(resolve => setTimeout(resolve, 20));
  }
  assert.ok(recovered, 'worker slot should be released without waiting 30 seconds');
});
