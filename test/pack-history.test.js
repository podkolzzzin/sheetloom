import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir, symlink, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { compilePack, readManifest } from '../src/pack.js';
import { renderXml } from '../src/render.js';
import { makePack, compile, worksheet } from './helpers/pack.js';

test('historical Moment locale side-effect alone configures the same imported instance', async t => {
  const fixture = await makePack(t, {
    'setup.ts': "import 'moment/locale/uk';",
    'helper.ts': "import moment from 'moment';export const month=moment('2026-01-15').format('MMMM');",
    'Report.vue': `<script setup>import {month} from './helper';</script>${worksheet('{{month}}')}`,
  }, { apiVersion: 1, templates: { report: 'Report.vue' }, setup: 'setup.ts' });
  const pack = await compile(t, fixture);
  assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /січень/);
});

for (const [name, source] of [
  ['async setup rejection', `<script setup>await Promise.resolve();throw Error('PRIVATE_ASYNC');</script>${worksheet('partial output')}`],
  ['falsy async exception', `<script setup>await Promise.resolve();throw undefined;</script>${worksheet('partial output')}`],
  ['render expression failure', `<script setup>const data=null;</script>${worksheet('{{data.private.value}}')}`],
  ['setup module exception', `<script setup>import './failed';</script>${worksheet('partial output')}`],
]) {
  test(`Vue production ${name} becomes safe failure rather than a partial workbook`, async t => {
    const fixture = await makePack(t, { 'Report.vue': source, 'failed.ts': "throw Error('PRIVATE_SETUP');" });
    const pack = await compile(t, fixture);
    await assert.rejects(renderXml(pack.bundle, 'report', {}, 10000), error => error.status === 422 && !/PRIVATE|partial/.test(error.message));
  });
}

test('setup executes once per request worker and cannot leak mutation into another report', async t => {
  const fixture = await makePack(t, {
    'setup.ts': 'globalThis.regressionCount=10;',
    'Report.vue': `<script setup>const n=++globalThis.regressionCount;</script>${worksheet('{{n}}')}`,
  }, { apiVersion: 1, templates: { report: 'Report.vue', second: 'Report.vue' }, setup: 'setup.ts' });
  const pack = await compile(t, fixture);
  for (const id of ['report', 'second', 'report']) assert.match(await renderXml(pack.bundle, id, {}, 10000), />11<\/Cell>/);
});

test('recompilation sees changed sources; disposing one cache leaves another valid', async t => {
  const fixture = await makePack(t, { 'Report.vue': worksheet('FIRST') });
  const original = await compile(t, fixture);
  await writeFile(path.join(fixture.directory, 'Report.vue'), worksheet('SECOND'));
  const replacement = await compile(t, fixture);
  assert.notEqual(original.bundle, replacement.bundle);
  assert.match(await renderXml(original.bundle, 'report', {}, 10000), /FIRST/);
  assert.match(await renderXml(replacement.bundle, 'report', {}, 10000), /SECOND/);
  await original.dispose(); await original.dispose();
  assert.match(await renderXml(replacement.bundle, 'report', {}, 10000), /SECOND/);
  assert.equal((await readdir(fixture.cache)).length, 1);
});

test('malformed manifest JSON, directory entries and non-Vue files fail before cache generation', async t => {
  for (const entry of ['directory.vue', 'module.ts']) {
    const fixture = await makePack(t, { 'module.ts': 'export default {};' }, { apiVersion: 1, templates: { report: entry } });
    await mkdir(path.join(fixture.directory, 'directory.vue'));
    await assert.rejects(compilePack(fixture.directory, fixture.cache));
  }
  const fixture = await makePack(t, { 'Report.vue': worksheet('OK') });
  await writeFile(path.join(fixture.directory, 'manifest.json'), '\uFEFF{"apiVersion":');
  await assert.rejects(readManifest(fixture.directory), SyntaxError);
});

test('contained template and asset symlinks remain supported', async t => {
  const fixture = await makePack(t, {
    'Actual.vue': `<script setup>import {readFile} from 'node:fs/promises';const text=await readFile(new URL('./asset-link',import.meta.url),'utf8');</script>${worksheet('{{text}}')}`,
    'asset': 'CONTAINED',
  });
  await symlink('Actual.vue', path.join(fixture.directory, 'Report.vue'));
  await symlink('asset', path.join(fixture.directory, 'asset-link'));
  const pack = await compile(t, fixture);
  assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /CONTAINED/);
});

test('encoded traversal asset path is still rejected and incomplete cache is removed', async t => {
  const fixture = await makePack(t, {
    'Report.vue': `<script setup>const asset=new URL('./%2e%2e/outside',import.meta.url);</script>${worksheet('no')}`,
  });
  await writeFile(path.join(fixture.root, 'outside'), 'PRIVATE');
  await assert.rejects(compilePack(fixture.directory, fixture.cache));
  assert.deepEqual(await readdir(fixture.cache), []);
});

test('manifest supplied by escaping symlink is rejected before interpreting private content', async t => {
  const fixture = await makePack(t, { 'Report.vue': worksheet('OK') });
  const manifest = path.join(fixture.directory, 'manifest.json');
  const { unlink } = await import('node:fs/promises');
  await writeFile(path.join(fixture.root, 'outside.json'), await readFile(manifest));
  await unlink(manifest); await symlink('../outside.json', manifest);
  await assert.rejects(readManifest(fixture.directory), /symlink escapes/);
});
