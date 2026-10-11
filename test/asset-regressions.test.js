import test from 'node:test';
import assert from 'node:assert/strict';
import { renderXml } from '../src/render.js';
import { sourceAssets } from '../src/assets.js';
import { makePack, compile, worksheet } from './helpers/pack.js';

for (const extension of ['js', 'mjs', 'ts']) {
  test(`source asset reads retain module-relative paths in ${extension} helpers`, async t => {
    const annotation = extension === 'ts' ? ': string' : '';
    const fixture = await makePack(t, {
      'Report.vue': `<script setup>import {read} from './nested/helper.${extension}';const text=await read();</script>${worksheet('{{text}}')}`,
      [`nested/helper.${extension}`]: `import {readFile} from 'node:fs/promises';export async function read(){const text${annotation}=await readFile(new URL('../assets/value.txt',import.meta.url),'utf8');return text;}`,
      'assets/value.txt': 'Україна & assets',
    });
    const pack = await compile(t, fixture);
    assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /Україна &amp; assets/);
  });
}

test('TypeScript angle-bracket assertions are valid when a module uses import.meta', async t => {
  const fixture = await makePack(t, {
    'Report.vue': `<script setup>import {text} from './helper.ts';</script>${worksheet('{{text}}')}`,
    'helper.ts': `import {readFileSync} from 'node:fs';const label=<string>'TS';export const text=label+readFileSync(new URL('./asset.txt',import.meta.url),'utf8');`,
    'asset.txt': ' asset',
  });
  const pack = await compile(t, fixture);
  assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /TS asset/);
});

test('asset rewriting obeys URL percent-decoding and keeps query and fragment metadata', async t => {
  const fixture = await makePack(t, {
    'Report.vue': `<script setup>import {readFile} from 'node:fs/promises';const url=new URL('./assets/a%20b.txt?version=1#part',import.meta.url);const text=await readFile(url,'utf8');</script>${worksheet('{{text}} {{url.search}} {{url.hash}}')}`,
    'assets/a b.txt': 'space asset',
  });
  const pack = await compile(t, fixture);
  assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /space asset \?version=1 #part/);
});

test('multiple assets across normal and setup scripts preserve replacement offsets', async t => {
  const fixture = await makePack(t, {
    'Report.vue': `<script lang="ts">export const first=new URL('./first.txt',import.meta.url);</script><script setup lang="ts">import {readFile} from 'node:fs/promises';const second=new URL('./second.txt',import.meta.url);const text=(await readFile(first,'utf8'))+(await readFile(second,'utf8'));</script>${worksheet('{{text}}')}`,
    'first.txt': 'FIRST', 'second.txt': 'SECOND',
  });
  const pack = await compile(t, fixture);
  assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /FIRSTSECOND/);
});

test('AST extraction ignores text examples while recognizing actual JSX and TSX scripts', () => {
  for (const filename of ['view.jsx', 'view.tsx']) {
    const source = `const el=<div>new URL('./not-an-asset', import.meta.url)</div>;const asset=new URL('./actual',import.meta.url);`;
    assert.deepEqual(sourceAssets(source, filename).map(x => x.relative), ['./actual']);
  }
  assert.deepEqual(sourceAssets(`// import.meta.url\nconst url="new URL('./string',import.meta.url)";const regex=/new URL/;`, 'helper.js'), []);
});

test('Vue lang=ts accepts angle assertions without enabling JSX parsing', async t => {
  const fixture = await makePack(t, {
    'Report.vue': `<script setup lang="ts">import {readFileSync} from 'node:fs';const label=<string>'VUE';const text=label+readFileSync(new URL('./asset.txt',import.meta.url),'utf8');</script>${worksheet('{{text}}')}`,
    'asset.txt': ' typescript',
  });
  const pack = await compile(t, fixture);
  assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /VUE typescript/);
});

for (const extension of ['vue', 'ts']) {
  test(`static backtick asset URLs use the mounted source path in ${extension}`, async t => {
    const script = "import {readFileSync} from 'node:fs';const text=readFileSync(new URL(`./asset.txt`,import.meta.url),'utf8');";
    const files = extension === 'vue'
      ? { 'Report.vue': `<script setup>${script}</script>${worksheet('{{text}}')}` }
      : { 'Report.vue': `<script setup>import {text} from './helper.ts';</script>${worksheet('{{text}}')}`, 'helper.ts': script + 'export {text};' };
    const fixture = await makePack(t, { ...files, 'asset.txt': 'Static backtick asset' });
    const pack = await compile(t, fixture);
    assert.match(await renderXml(pack.bundle, 'report', {}, 10000), /Static backtick asset/);
  });
}

test('static template literal extraction uses cooked escapes and leaves dynamic or tagged literals alone', () => {
  const source = 'const a=new URL(`./a\\u0020b.txt`,import.meta.url);const b=new URL(`./${name}.txt`,import.meta.url);const c=new URL(String.raw`./raw.txt`,import.meta.url);';
  assert.deepEqual(sourceAssets(source, 'helper.js').map(x => x.relative), ['./a b.txt']);
});
