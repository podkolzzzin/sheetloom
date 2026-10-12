import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { renderXml } from '../../src/render.js';

async function moduleFile(t, source) {
  const root = await mkdtemp(path.join(tmpdir(), 'engine-worker-history-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  const filename = path.join(root, 'bundle.mjs');
  await writeFile(filename, source);
  return { root, filename, url: pathToFileURL(filename).href };
}
for (const [name, source] of [
  ['syntax error', 'export function render( { PRIVATE_SYNTAX'],
  ['missing render export', 'export const data="PRIVATE_EXPORT";'],
  ['missing module import', 'import "./PRIVATE_MISSING.mjs"; export function render(){}'],
  ['unexpected normal process exit', 'export function render(){process.exit(0)}'],
  ['unexpected error process exit', 'export function render(){process.exit(3)}'],
  ['top-level import rejection', 'await Promise.reject(Error("PRIVATE_IMPORT"));'],
  ['uncloneable render result', 'export function render(){return ()=>"PRIVATE_FUNCTION"}'],
]) {
  test(`broken worker ${name} rejects safely and does not poison a later worker`, async t => {
    const broken = await moduleFile(t, source);
    await assert.rejects(renderXml(broken.url, 'report', {}, 5000), error => error.status === 422 && !error.message.includes('PRIVATE'));
    const good = await moduleFile(t, 'export function render(){return "<Workbook/>"}');
    assert.equal(await renderXml(good.url, 'report', {}, 5000), '<Workbook/>');
  });
}

test('worker standard output, standard error and thrown payload secrets never reach process logs', async t => {
  const bundle = await moduleFile(t, `export function render(id,data){console.log(data);console.error(data);process.stderr.write(data);throw Error(data)}`);
  const runner = path.join(bundle.root, 'runner.mjs');
  const rendererModule = new URL('../../src/render.js', import.meta.url).href;
  await writeFile(runner, `import {renderXml} from ${JSON.stringify(rendererModule)};try{await renderXml(${JSON.stringify(bundle.url)},'report','PRIVATE_PAYLOAD_CANARY',5000)}catch(error){console.log(error.status,error.code,error.message)}`);
  const { stdout, stderr } = await promisify(execFile)(process.execPath, [runner], { timeout: 10000 });
  assert.match(stdout, /422 template_failed/);
  assert.doesNotMatch(stdout + stderr, /PRIVATE_PAYLOAD_CANARY/);
});

test('late asynchronous handles cannot survive a completed render worker', async t => {
  const bundle = await moduleFile(t, `import {writeFileSync} from 'node:fs';export function render(id,data){setTimeout(()=>writeFileSync(data,'LEAK'),300);return '<Workbook/>'}`);
  const marker = path.join(bundle.root, 'late.txt');
  assert.equal(await renderXml(bundle.url, 'report', marker, 5000), '<Workbook/>');
  await new Promise(resolve => setTimeout(resolve, 400));
  const { access } = await import('node:fs/promises');
  await assert.rejects(access(marker), { code: 'ENOENT' });
});
