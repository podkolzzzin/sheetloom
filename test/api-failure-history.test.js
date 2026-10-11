import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer as httpServer } from 'node:http';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createServer } from '../src/server.js';

async function api(t, handler, options = {}, source = 'export function render(id,data){return "<Workbook>"+JSON.stringify(data)+"</Workbook>"}') {
  const directory = await mkdtemp(path.join(tmpdir(), 'engine-api-history-'));
  const bundle = path.join(directory, 'bundle.mjs');
  await writeFile(bundle, source);
  const received = [];
  const renderer = httpServer(async (request, response) => {
    let text = '';
    for await (const chunk of request) text += chunk;
    if (request.url === '/render') received.push(JSON.parse(text));
    handler(request, response, received);
  });
  await new Promise(resolve => renderer.listen(0, '127.0.0.1', resolve));
  const app = createServer({ pack: { ids: ['report'], bundle: pathToFileURL(bundle).href }, rendererUrl: `http://127.0.0.1:${renderer.address().port}`, ...options });
  t.after(async () => {
    await app.close();
    renderer.closeAllConnections();
    await new Promise(resolve => renderer.close(resolve));
    await rm(directory, { recursive: true, force: true });
  });
  return { app, renderer, received };
}
const post = (app, data) => app.inject({ method: 'POST', url: '/v1/render/report', payload: { data } });
const success = (request, response) => response.end('PK-test-workbook');

for (const mode of ['connection refusal', 'HTTP failure', 'headers without body completion']) {
  test(`health reports 503 on renderer ${mode}`, async t => {
    const { app, renderer } = await api(t, (request, response) => {
      if (mode === 'HTTP failure') { response.statusCode = 500; response.end('PRIVATE'); }
      else if (mode === 'headers without body completion') { response.writeHead(503); response.flushHeaders(); }
    });
    if (mode === 'connection refusal') await new Promise(resolve => renderer.close(resolve));
    const reply = await app.inject('/health');
    assert.equal(reply.statusCode, 503);
    assert.deepEqual(reply.json(), { error: { code: 'renderer_unavailable', message: 'Renderer unavailable' } });
  });
}

for (const mode of ['before response headers', 'after partial response body']) {
  test(`renderer timeout ${mode} produces 504 and releases public capacity`, async t => {
    const { app, received } = await api(t, (request, response, received) => {
      if (received.length === 1) {
        if (mode === 'after partial response body') { response.writeHead(200); response.write('PK'); }
      } else success(request, response);
    }, { timeoutMs: 1000, concurrency: 1 });
    const timedOut = await post(app, 'first');
    assert.equal(timedOut.statusCode, 504);
    assert.equal(timedOut.json().error.code, 'render_timeout');
    const recovered = await post(app, 'second');
    assert.equal(recovered.statusCode, 200);
    assert.equal(received.length, 2);
  });
}

test('one deadline covers both slow SSR and slow renderer work', async t => {
  const source = 'export async function render(){await new Promise(r=>setTimeout(r,700));return "<Workbook/>"}';
  const { app } = await api(t, (request, response) => {
    const timer = setTimeout(() => response.end('PK-workbook'), 700);
    response.on('close', () => clearTimeout(timer));
  }, { timeoutMs: 1200 }, source);
  const response = await post(app, null);
  assert.equal(response.statusCode, 504);
});

test('real HTTP renderer timeout preserves a structured response after partial upstream bytes', async t => {
  const { app } = await api(t, (request, response) => response.write('PK'), { timeoutMs: 700 });
  await app.listen({ host: '127.0.0.1', port: 0 });
  const response = await fetch(`http://127.0.0.1:${app.server.address().port}/v1/render/report`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{"data":0}', signal: AbortSignal.timeout(5000),
  });
  assert.equal(response.status, 504);
  assert.equal((await response.json()).error.code, 'render_timeout');
});

test('multi-byte XML output limit is measured in UTF-8 bytes, not character count', async t => {
  const { app, received } = await api(t, success, {}, `export function render(id,data){return data==='huge'?'ї'.repeat(4*1024*1024+1):'<Workbook/>'}`);
  const rejected = await post(app, 'huge');
  assert.equal(rejected.statusCode, 413);
  assert.equal(rejected.json().error.code, 'output_too_large');
  assert.equal(received.length, 0);
  assert.equal((await post(app, 'small')).statusCode, 200);
});

test('exact XML byte limit passes without an off-by-one rejection', async t => {
  const { app, received } = await api(t, success, {}, `export function render(){return 'x'.repeat(8*1024*1024)}`);
  assert.equal((await post(app, null)).statusCode, 200);
  assert.equal(Buffer.byteLength(received[0].xml), 8*1024*1024);
});

test('sequential failure classes all release the same render slot', async t => {
  const { app } = await api(t, (request, response, received) => {
    if (received.length === 1) { response.statusCode = 422; response.end('PRIVATE'); }
    else if (received.length === 2) response.destroy();
    else success(request, response);
  }, { concurrency: 1 });
  assert.equal((await post(app, 1)).statusCode, 422);
  assert.equal((await post(app, 2)).statusCode, 502);
  assert.equal((await post(app, 3)).statusCode, 200);
});

test('wrong content type and JSON scalar envelopes fail before invoking renderer', async t => {
  const { app, received } = await api(t, success);
  const wrongType = await app.inject({ method: 'POST', url: '/v1/render/report', headers: { 'Content-Type': 'application/octet-stream' }, payload: 'PRIVATE' });
  assert.equal(wrongType.statusCode, 415); assert.doesNotMatch(wrongType.body, /PRIVATE/);
  for (const payload of ['null', '12', '"text"', 'true']) {
    const reply = await app.inject({ method: 'POST', url: '/v1/render/report', headers: { 'Content-Type': 'application/json' }, payload });
    assert.equal(reply.statusCode, 400);
  }
  assert.equal(received.length, 0);
});

test('primitive, null and array data reach templates without coercion', async t => {
  const { app, received } = await api(t, success);
  for (const value of [0, false, '', null, ['ї', 2]]) assert.equal((await post(app, value)).statusCode, 200);
  assert.deepEqual(received.map(x => x.xml), [0, false, '', null, ['ї', 2]].map(x => '<Workbook>'+JSON.stringify(x)+'</Workbook>'));
});

test('failed readiness cancels an unfinished upstream body immediately', async t => {
  let cancelled;
  const closed = new Promise(resolve => { cancelled = resolve; });
  const { app } = await api(t, (request, response) => {
    response.on('close', () => cancelled(true));
    response.writeHead(503); response.flushHeaders();
  });
  assert.equal((await app.inject('/health')).statusCode, 503);
  let timer;
  try {
    assert.equal(await Promise.race([closed, new Promise(resolve => { timer = setTimeout(() => resolve(false), 500); })]), true);
  } finally { clearTimeout(timer); }
});
