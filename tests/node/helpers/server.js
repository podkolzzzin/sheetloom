import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createServer } from '../../../src/server.js';

export async function setup(t, options = {}, source) {
  const dir = await mkdtemp(path.join(tmpdir(), 'template-api-'));
  const file = path.join(dir, 'bundle.mjs');
  await writeFile(file, source ?? `export async function render(id,data){if(data==='throw')throw Error('SECRET');if(data==='hang'){while(true){}}if(data==='slow')await new Promise(r=>setTimeout(r,300));return '<Workbook>'+JSON.stringify(data)+'</Workbook>'}`);
  const app = createServer({ pack: { ids: ['invoice'], bundle: pathToFileURL(file).href }, ...options });
  t.after(async () => { await app.close(); await rm(dir, { recursive: true, force: true }); });
  return app;
}
