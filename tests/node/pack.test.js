import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, mkdir, rm, symlink, readdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { compilePack, readManifest } from '../../src/pack.js';
import { renderXml } from '../../src/render.js';

async function fixture(t, manifest, files = {}) {
  const root = await mkdtemp(path.join(tmpdir(), 'engine-test-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  const pack = path.join(root, 'pack'); await mkdir(pack);
  await writeFile(path.join(pack, 'manifest.json'), '\uFEFF' + JSON.stringify(manifest));
  for (const [name, content] of Object.entries(files)) {
    await mkdir(path.dirname(path.join(pack, name)), { recursive: true });
    await writeFile(path.join(pack, name), content);
  }
  return { root, pack, cache: path.join(root, 'cache') };
}
const simple = '<template><Workbook><Worksheet name="Test"><Row><Cell>Hello</Cell></Row></Worksheet></Workbook></template>';

test('manifest rejects malformed versions, maps, IDs and unknown fields', async t => {
  for (const manifest of [null, {}, {apiVersion:2,templates:{x:'X.vue'}}, {apiVersion:1,templates:{}},
    {apiVersion:1,templates:[]}, {apiVersion:1,templates:{'../x':'X.vue'}},
    {apiVersion:1,templates:{x:'X.vue'},wrong:true}, {apiVersion:1,templates:{x:'X.vue'},aliases:[]}]) {
    const f = await fixture(t, manifest, {'X.vue':simple});
    await assert.rejects(readManifest(f.pack));
  }
});
test('manifest rejects traversal, absolute paths, symlinks and missing files', async t => {
  for (const target of ['../Outside.vue', '/etc/passwd', 'missing.vue', 'link.vue']) {
    const f = await fixture(t, {apiVersion:1,templates:{x:target}});
    await writeFile(path.join(f.root,'Outside.vue'),simple);
    await symlink(path.join(f.root,'Outside.vue'),path.join(f.pack,'link.vue'));
    await assert.rejects(readManifest(f.pack));
  }
});
test('manifest validates aliases and setup containment', async t => {
  for (const extra of [{aliases:{'@':'../'}}, {aliases:{vue:'.'}}, {setup:'../setup.ts'}, {setup:false}]) {
    const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'},...extra},{'X.vue':simple});
    await assert.rejects(readManifest(f.pack));
  }
});
test('compiles BOM manifest, aliases, TS, setup/locale and source assets outside engine', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'},aliases:{'@':'lib'},setup:'setup.ts'}, {
    'setup.ts':"import moment from 'moment'; import 'moment/locale/uk'; moment.locale('uk');",
    'lib/value.ts':'export const prefix: string = "Вітаю";',
    'asset.txt':'asset bytes',
    'X.vue':`<script setup lang="ts">
      import {inject} from 'vue'; import moment from 'moment'; import {readFile} from 'node:fs/promises';
      import {prefix} from '@/value';
      const value = inject<any>('templateData');
      const asset = await readFile(new URL('./asset.txt', import.meta.url),'utf8');
      </script><template><Workbook><Worksheet name="Data"><Row><Cell>{{prefix}} {{value}} {{moment('2026-01-01').format('MMMM')}} {{asset}}</Cell></Row></Worksheet></Workbook></template>`,
  });
  const pack=await compilePack(f.pack,f.cache); t.after(pack.dispose);
  assert.deepEqual(pack.ids,['x']);
  const xml=await renderXml(pack.bundle,'x','<&Україна>',10000);
  assert.match(xml,/Вітаю &lt;&amp;Україна&gt; січень asset bytes/);
  assert.deepEqual((await readdir(f.pack)).sort(),['X.vue','asset.txt','lib','manifest.json','setup.ts']);
});
test('fresh workers isolate module state for concurrent requests', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{
    'state.ts':'export let n=0; export function next(){return ++n}',
    'X.vue':`<script setup>import {inject} from 'vue';import {next} from './state';const n=next();const data=inject('templateData');</script><template><Workbook><Worksheet name="Test"><Row><Cell>{{n}}:{{data}}</Cell></Row></Worksheet></Workbook></template>`,
  });
  const pack=await compilePack(f.pack,f.cache);t.after(pack.dispose);
  const results=await Promise.all(['A','B','C'].map(x=>renderXml(pack.bundle,'x',x,10000)));
  results.forEach((xml,i)=>assert.match(xml,new RegExp(`1:${['A','B','C'][i]}`)));
});
test('compile failure removes cache and rejects imports outside pack', async t => {
  for (const source of ["import './missing'", "import '../outside.js'"]) {
    const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{'X.vue':`<script setup>${source}</script><template><Workbook /></template>`});
    await writeFile(path.join(f.root,'outside.js'),'export const x=1');
    await assert.rejects(compilePack(f.pack,f.cache));
    assert.deepEqual(await readdir(f.cache),[]);
  }
});
test('cache cannot live in read-only template tree', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{'X.vue':simple});
  await assert.rejects(compilePack(f.pack,path.join(f.pack,'cache')),/outside/);
});
test('worker timeout terminates CPU-bound template and future requests still work', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{'X.vue':`<script setup>import {inject} from 'vue';if(inject('templateData')){while(true){}}</script><template><Workbook /></template>`});
  const pack=await compilePack(f.pack,f.cache);t.after(pack.dispose);
  await assert.rejects(renderXml(pack.bundle,'x',true,200),e=>e.status===504);
  assert.match(await renderXml(pack.bundle,'x',false,5000),/<Workbook/);
});
test('render errors are safe and exclude template or payload secrets', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{'X.vue':`<script setup>throw new Error('SECRET')</script><template><Workbook /></template>`});
  const pack=await compilePack(f.pack,f.cache);t.after(pack.dispose);
  await assert.rejects(renderXml(pack.bundle,'x',{},5000),e=>e.status===422 && !e.message.includes('SECRET'));
});
test('URL examples in template text, comments and strings remain ordinary text', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{'X.vue':`<script setup>
    // new URL('./missing-comment', import.meta.url)
    const text = "new URL('./missing-string', import.meta.url)";
    </script><template><Workbook><Worksheet name="Test"><Row><Cell>{{text}}</Cell><Cell>new URL('./missing-cell', import.meta.url)</Cell></Row></Worksheet></Workbook></template>`});
  const pack=await compilePack(f.pack,f.cache);t.after(pack.dispose);
  const xml=await renderXml(pack.bundle,'x',{},5000);
  assert.match(xml,/missing-string/);assert.match(xml,/missing-cell/);
});
test('asset symlinks and cache symlinks cannot redirect into forbidden locations', async t => {
  const f=await fixture(t,{apiVersion:1,templates:{x:'X.vue'}},{'X.vue':`<script setup>const asset=new URL('./asset',import.meta.url)</script><template><Workbook /></template>`});
  await writeFile(path.join(f.root,'outside'),'secret');
  await symlink(path.join(f.root,'outside'),path.join(f.pack,'asset'));
  await assert.rejects(compilePack(f.pack,f.cache));
  const redirect=path.join(f.root,'redirect');await symlink(f.pack,redirect);
  await assert.rejects(compilePack(f.pack,redirect),/outside/);
});
