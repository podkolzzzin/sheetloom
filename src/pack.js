import { readFile, realpath, stat, mkdir, mkdtemp, writeFile, rm } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { build } from 'vite';
import vue from '@vitejs/plugin-vue';
import { sourceAssets } from './assets.js';

const dependenciesRoot = fileURLToPath(new URL('../node_modules', import.meta.url));
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const inside = (root, target) => target === root || target.startsWith(root + path.sep);
const tags = new Set(['workbook', 'worksheet', 'row', 'cell', 'formular1c1', 'printingarea', 'namedrange', 'lastpagerow', 'rowstorepeatattop']);

async function packPath(root, value, directory = false) {
  if (typeof value !== 'string' || !value || path.isAbsolute(value)) throw new Error('Pack paths must be relative');
  const candidate = path.resolve(root, value);
  if (!inside(root, candidate)) throw new Error('Pack path escapes template directory');
  const resolved = await realpath(candidate);
  if (!inside(root, resolved)) throw new Error('Pack symlink escapes template directory');
  const info = await stat(resolved);
  if (directory ? !info.isDirectory() : !info.isFile()) throw new Error('Invalid pack path type');
  return resolved;
}

export async function readManifest(directory) {
  const root = await realpath(directory);
  const manifestFile = await packPath(root, 'manifest.json');
  const manifest = JSON.parse((await readFile(manifestFile, 'utf8')).replace(/^\uFEFF/, ''));
  if (!object(manifest) || manifest.apiVersion !== 1 || !object(manifest.templates) || !Object.keys(manifest.templates).length) {
    throw new Error('Expected manifest apiVersion 1 and nonempty templates');
  }
  if (Object.keys(manifest).some(key => !['apiVersion', 'templates', 'aliases', 'setup'].includes(key))) throw new Error('Unknown manifest field');
  const templates = Object.create(null);
  for (const [id, value] of Object.entries(manifest.templates)) {
    if (!/^[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}$/.test(id)) throw new Error('Invalid template ID');
    templates[id] = await packPath(root, value);
    if (!templates[id].endsWith('.vue')) throw new Error('Template entry must be a Vue file');
  }
  const aliases = [];
  if (manifest.aliases !== undefined && !object(manifest.aliases)) throw new Error('Invalid aliases');
  for (const [find, value] of Object.entries(manifest.aliases ?? {})) {
    if (!/^@[a-zA-Z0-9_-]*$/.test(find)) throw new Error('Aliases must start with @');
    aliases.push({ find, replacement: await packPath(root, value, true) });
  }
  const setup = manifest.setup === undefined ? null : await packPath(root, manifest.setup);
  return { root, templates, aliases, setup };
}

export async function compilePack(directory, cacheDirectory) {
  const manifest = await readManifest(directory);
  const cacheRoot = path.resolve(cacheDirectory);
  if (inside(manifest.root, cacheRoot)) throw new Error('Cache must be outside template pack');
  await mkdir(cacheRoot, { recursive: true });
  if (inside(manifest.root, await realpath(cacheRoot))) throw new Error('Cache must be outside template pack');
  const cache = await mkdtemp(path.join(cacheRoot, 'pack-'));
  try {
    const imports = Object.values(manifest.templates).map((file, index) => `import T${index} from ${JSON.stringify(file)};`);
    const entries = Object.keys(manifest.templates).map((id, index) => `${JSON.stringify(id)}:T${index}`);
    const source = `${manifest.setup ? `import ${JSON.stringify(manifest.setup)};` : ''}
      import {createSSRApp} from 'vue';
      import {renderToString} from 'vue/server-renderer';
      ${imports.join('\n')}
      const templates = {${entries.join(',')}};
      export async function render(id, data) {
        const app = createSSRApp(templates[id]);
        app.config.throwUnhandledErrorInProduction = true;
        // Vue SSR can swallow rejected async setup promises. Record its handled
        // errors and reject the whole render rather than returning a partial workbook.
        let failed = false, failure;
        app.config.errorHandler = error => { failed = true; failure = error; };
        app.provide('ssrRequest', {body: data});
        const xml = await renderToString(app, {});
        if (failed) throw failure;
        return xml;
      }`;
    const entry = path.join(cache, 'entry.mjs');
    await writeFile(entry, source);
    // Resolve engine-owned packages even when the read-only pack is outside this project.
    const dependencies = ['vue', 'vue-router', 'moment', 'linq'].map(name => ({
      find: name,
      replacement: path.join(dependenciesRoot, name),
    }));
    await build({
      configFile: false, root: manifest.root, logLevel: 'warn',
      resolve: { alias: [...manifest.aliases, { find: /^moment$/, replacement: path.join(dependenciesRoot, 'moment/moment.js') }, ...dependencies] },
      plugins: [{
        name: 'pack-source-assets', enforce: 'pre',
        async transform(source, id) {
          const filename = id.split('?')[0];
          if (!inside(manifest.root, filename)) return;
          for (const reference of sourceAssets(source, filename)) {
            const originalUrl = new URL(reference.relative, pathToFileURL(filename));
            const asset = await packPath(manifest.root, path.relative(manifest.root, fileURLToPath(originalUrl)));
            const assetUrl = pathToFileURL(asset);
            assetUrl.search = originalUrl.search;
            assetUrl.hash = originalUrl.hash;
            source = source.slice(0, reference.start) + `new URL(${JSON.stringify(assetUrl.href)})` + source.slice(reference.end);
          }
          return source;
        },
        async load(id) {
          const filename = id.split('?')[0];
          if (!path.isAbsolute(filename)) return;
          const resolved = await realpath(filename);
          if (![manifest.root, dependenciesRoot, cache].some(root => inside(root, resolved))) throw new Error('Import escapes template directory');
        },
      }, vue({ template: { compilerOptions: { isCustomElement: tag => tags.has(tag.toLowerCase()) } } })],
      ssr: { noExternal: true },
      build: { ssr: entry, outDir: path.join(cache, 'dist'), emptyOutDir: true, minify: false,
        rollupOptions: { output: { entryFileNames: 'render.mjs' } } },
    });
    return { ids: Object.keys(manifest.templates), bundle: pathToFileURL(path.join(cache, 'dist/render.mjs')).href,
      dispose: () => rm(cache, { recursive: true, force: true }) };
  } catch (error) {
    await rm(cache, { recursive: true, force: true });
    throw error;
  }
}
