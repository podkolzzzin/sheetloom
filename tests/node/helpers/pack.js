import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { compilePack } from '../../../src/pack.js';

export const worksheet = content => `<template><Workbook><Worksheet name="Regression"><Row><Cell>${content}</Cell></Row></Worksheet></Workbook></template>`;
export async function makePack(t, files, manifest = { apiVersion: 1, templates: { report: 'Report.vue' } }) {
  const root = await mkdtemp(path.join(tmpdir(), 'engine-regression-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  const directory = path.join(root, 'pack');
  await mkdir(directory);
  await writeFile(path.join(directory, 'manifest.json'), '\uFEFF' + JSON.stringify(manifest));
  for (const [filename, source] of Object.entries(files)) {
    await mkdir(path.dirname(path.join(directory, filename)), { recursive: true });
    await writeFile(path.join(directory, filename), source);
  }
  return { root, directory, cache: path.join(root, 'cache') };
}
export async function compile(t, fixture) {
  const pack = await compilePack(fixture.directory, fixture.cache);
  t.after(pack.dispose);
  return pack;
}
