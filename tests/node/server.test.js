import test from 'node:test';
import assert from 'node:assert/strict';
import { setup } from './helpers/server.js';

const post = (app, data) => app.inject({ method: 'POST', url: '/render/invoice', payload: { data } });

test('private service returns XML and preserves primitive, null and array data', async t => {
  const app = await setup(t);
  for (const data of [0, false, '', null, ['ї', 2], { text: 'example' }]) {
    const response = await post(app, data);
    assert.equal(response.statusCode, 200);
    assert.match(response.headers['content-type'], /application\/xml/);
    assert.equal(response.body, '<Workbook>'+JSON.stringify(data)+'</Workbook>');
    assert.equal(response.headers['content-disposition'], undefined);
  }
});
test('private health and discovery do not expose the public API', async t => {
  const app = await setup(t);
  assert.deepEqual((await app.inject('/health')).json(), { status: 'ok' });
  assert.deepEqual((await app.inject('/templates')).json(), { templates: ['invoice'] });
  assert.equal((await app.inject('/v1/templates')).statusCode, 404);
  assert.equal((await app.inject({ method: 'POST', url: '/v1/render/invoice', payload: { data: 0 } })).statusCode, 404);
});
test('private envelope validation rejects options, scalar bodies and malformed JSON', async t => {
  const app = await setup(t);
  for (const payload of [{}, [], null, 12, true, { data: 1, options: {} }]) {
    const response = await app.inject({ method: 'POST', url: '/render/invoice', headers: { 'content-type': 'application/json' }, payload: JSON.stringify(payload) });
    assert.equal(response.statusCode, 400);
  }
  assert.equal((await app.inject({ method: 'POST', url: '/render/invoice', headers: { 'content-type': 'application/json' }, payload: '{' })).statusCode, 400);
  assert.equal((await app.inject({ method: 'POST', url: '/render/invoice', headers: { 'content-type': 'application/octet-stream' }, payload: 'SECRET' })).statusCode, 415);
});
test('unknown templates and excessive bodies have structured errors', async t => {
  const app = await setup(t, { bodyLimit: 100 });
  assert.equal((await app.inject({ method: 'POST', url: '/render/missing', payload: { data: 0 } })).json().error.code, 'unknown_template');
  assert.equal((await post(app, 'x'.repeat(200))).json().error.code, 'body_too_large');
});
test('template failures hide input and release capacity', async t => {
  const app = await setup(t, { concurrency: 1 });
  const bad = await post(app, 'throw');
  assert.equal(bad.statusCode, 422); assert.doesNotMatch(bad.body, /SECRET/);
  assert.equal((await post(app, 'good')).statusCode, 200);
});
test('overlapping renders reject excess work and recover', async t => {
  const app = await setup(t, { concurrency: 1 });
  const first = post(app, 'slow');
  await new Promise(resolve => setTimeout(resolve, 50));
  assert.equal((await post(app, 'next')).statusCode, 503);
  assert.equal((await first).statusCode, 200);
  assert.equal((await post(app, 'next')).statusCode, 200);
});
test('real HTTP worker timeout returns structured 504 and releases its slot', async t => {
  const app = await setup(t, { timeoutMs: 1000, concurrency: 1 });
  await app.listen({ host: '127.0.0.1', port: 0 });
  const response = await fetch(`http://127.0.0.1:${app.server.address().port}/render/invoice`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: '{"data":"hang"}', signal: AbortSignal.timeout(10000),
  });
  assert.equal(response.status, 504);
  assert.equal((await response.json()).error.code, 'render_timeout');
  assert.equal((await post(app, 'good')).statusCode, 200);
});
