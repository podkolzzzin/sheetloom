import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer as httpServer } from 'node:http';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createServer } from '../src/server.js';

async function setup(t, options = {}, handler) {
  const dir=await mkdtemp(path.join(tmpdir(),'engine-api-'));t.after(()=>rm(dir,{recursive:true,force:true}));
  const file=path.join(dir,'bundle.mjs');
  await writeFile(file, `export async function render(id,data){if(data==='throw')throw Error('SECRET');if(data==='hang'){while(true){}}if(data==='slow')await new Promise(r=>setTimeout(r,200));return '<Workbook>'+JSON.stringify(data)+'</Workbook>'}`);
  const received=[];
  const renderer=httpServer(async (req,res)=>{
    if(req.url==='/health'){res.end('{}');return;}
    let body='';for await(const chunk of req)body+=chunk;
    received.push(JSON.parse(body));
    if(handler){handler(req,res);return;}
    res.setHeader('Content-Type','application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');res.end(Buffer.from('PK-XLSX-test'));
  });
  await new Promise(resolve=>renderer.listen(0,'127.0.0.1',resolve));
  t.after(()=>new Promise(resolve=>renderer.close(resolve)));
  const rendererUrl=`http://127.0.0.1:${renderer.address().port}`;
  const app=createServer({pack:{ids:['invoice'],bundle:pathToFileURL(file).href},rendererUrl,...options});
  t.after(()=>app.close());
  return {app,received,renderer};
}
const post=(app,payload)=>app.inject({method:'POST',url:'/v1/render/invoice',payload});

test('API passes data and precision option, returns attachment with XLSX media type',async t=>{
  const {app,received}=await setup(t);
  const response=await post(app,{data:{text:'Україна & <'},options:{precisionAsDisplayed:true}});
  assert.equal(response.statusCode,200);assert.equal(response.rawPayload.toString(),'PK-XLSX-test');
  assert.match(response.headers['content-type'],/spreadsheetml.sheet/);
  assert.equal(response.headers['content-disposition'],'attachment; filename="invoice.xlsx"');
  assert.equal(response.headers['cache-control'],'no-store');
  assert.deepEqual(received,[{xml:'<Workbook>{"text":"Україна & <"}</Workbook>',precisionAsDisplayed:true}]);
});
test('default precision and explicit false remain false',async t=>{
  const {app,received}=await setup(t);
  for(const payload of [{data:null},{data:[],options:{precisionAsDisplayed:false}}])assert.equal((await post(app,payload)).statusCode,200);
  assert.ok(received.every(x=>x.precisionAsDisplayed===false));
});
test('readiness and template discovery',async t=>{
  const {app}=await setup(t);
  assert.deepEqual((await app.inject('/health')).json(),{status:'ok'});
  assert.deepEqual((await app.inject('/v1/templates')).json(),{templates:['invoice']});
});
test('validates JSON envelope and option types without silent coercion',async t=>{
  const {app}=await setup(t);
  for(const payload of [{},[],{data:1,extra:2},{data:1,options:null},{data:1,options:[]},{data:1,options:{precisionAsDisplayed:'true'}},{data:1,options:{precisionAsDisplayed:1}},{data:1,options:{other:true}}]){
    assert.equal((await post(app,payload)).statusCode,400,JSON.stringify(payload));
  }
  const bad=await app.inject({method:'POST',url:'/v1/render/invoice',headers:{'content-type':'application/json'},payload:'{"data":'});
  assert.equal(bad.statusCode,400);assert.equal(bad.json().error.code,'invalid_request');
});
test('unknown templates/routes use consistent 404 errors',async t=>{
  const {app}=await setup(t);
  const missing=await app.inject({method:'POST',url:'/v1/render/missing',payload:{data:{}}});
  assert.equal(missing.statusCode,404);assert.equal(missing.json().error.code,'unknown_template');
  assert.equal((await app.inject('/missing')).json().error.code,'not_found');
});
test('body size is bounded',async t=>{
  const {app}=await setup(t,{bodyLimit:100});
  const response=await post(app,{data:'x'.repeat(200)});
  assert.equal(response.statusCode,413);assert.equal(response.json().error.code,'body_too_large');
});
test('template failures omit sensitive content',async t=>{
  const {app}=await setup(t);
  const response=await post(app,{data:'throw'});
  assert.equal(response.statusCode,422);assert.doesNotMatch(response.body,/SECRET/);
});
test('capacity rejects overlapping requests and recovers',async t=>{
  const {app,received}=await setup(t,{concurrency:1});
  const first=post(app,{data:'slow'});await new Promise(r=>setTimeout(r,50));
  assert.equal((await post(app,{data:'next'})).statusCode,503);
  assert.equal((await first).statusCode,200);
  assert.equal((await post(app,{data:'next'})).statusCode,200);assert.equal(received.length,2);
});
test('template deadline releases render capacity',async t=>{
  const {app}=await setup(t,{timeoutMs:2000,concurrency:1});
  assert.equal((await post(app,{data:'hang'})).statusCode,504);
  assert.equal((await post(app,{data:'fast'})).statusCode,200);
});
test('real HTTP connection receives structured timeout instead of a socket reset',async t=>{
  const {app}=await setup(t,{timeoutMs:1000});
  await app.listen({host:'127.0.0.1',port:0});
  const response=await fetch(`http://127.0.0.1:${app.server.address().port}/v1/render/invoice`,{
    method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({data:'hang'}),signal:AbortSignal.timeout(10000),
  });
  assert.equal(response.status,504);assert.equal((await response.json()).error.code,'render_timeout');
});
test('renderer HTTP errors map to safe, stable responses',async t=>{
  for(const [source,expected] of [[400,422],[422,422],[500,502],[503,503],[413,413]]){
    const {app}=await setup(t,{},(req,res)=>{res.statusCode=source;res.end('SECRET');});
    const response=await post(app,{data:{}});assert.equal(response.statusCode,expected);assert.doesNotMatch(response.body,/SECRET/);
  }
});
test('renderer disconnection and invalid output never return a successful workbook',async t=>{
  for(const handler of [(req,res)=>res.destroy(),(req,res)=>res.end('not xlsx')]){
    const {app}=await setup(t,{},handler);assert.equal((await post(app,{data:{}})).statusCode,502);
  }
});
