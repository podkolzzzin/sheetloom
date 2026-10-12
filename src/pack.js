import { readFile, realpath, stat, mkdir, mkdtemp, writeFile, copyFile, rm } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { build } from 'vite';
import vue from '@vitejs/plugin-vue';
import { sourceAssets } from './assets.js';

const dependenciesRoot = fileURLToPath(new URL('../node_modules', import.meta.url));
const runtimeFile = fileURLToPath(new URL('./template-runtime.js', import.meta.url));
const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const isInside = (root, target) => target === root || target.startsWith(root + path.sep);
const workbookTags = new Set([
  'workbook', 'worksheet', 'row', 'cell', 'formular1c1', 'printingarea',
  'namedrange', 'lastpagerow', 'rowstorepeatattop',
]);

async function packPath(root, value, directory = false) {
  if (typeof value !== 'string' || !value || path.isAbsolute(value)) {
    throw new Error('Pack paths must be relative');
  }
  const candidate = path.resolve(root, value);
  if (!isInside(root, candidate)) {
    throw new Error('Pack path escapes template directory');
  }
  const resolved = await realpath(candidate);
  if (!isInside(root, resolved)) {
    throw new Error('Pack symlink escapes template directory');
  }
  const info = await stat(resolved);
  if (directory ? !info.isDirectory() : !info.isFile()) {
    throw new Error('Invalid pack path type');
  }
  return resolved;
}

export async function readManifest(directory) {
  const root = await realpath(directory);
  const manifestFile = await packPath(root, 'manifest.json');
  const manifest = JSON.parse((await readFile(manifestFile, 'utf8')).replace(/^\uFEFF/, ''));
  if (!isObject(manifest) || manifest.apiVersion !== 1 ||
      !isObject(manifest.templates) || !Object.keys(manifest.templates).length) {
    throw new Error('Expected manifest apiVersion 1 and nonempty templates');
  }
  const allowedFields = ['apiVersion', 'templates', 'aliases', 'setup'];
  if (Object.keys(manifest).some(key => !allowedFields.includes(key))) {
    throw new Error('Unknown manifest field');
  }

  const templates = Object.create(null);
  for (const [id, value] of Object.entries(manifest.templates)) {
    if (!/^[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}$/.test(id)) {
      throw new Error('Invalid template ID');
    }
    templates[id] = await packPath(root, value);
    if (!templates[id].endsWith('.vue')) {
      throw new Error('Template entry must be a Vue file');
    }
  }

  const aliases = [];
  if (manifest.aliases !== undefined && !isObject(manifest.aliases)) {
    throw new Error('Invalid aliases');
  }
  for (const [find, value] of Object.entries(manifest.aliases ?? {})) {
    if (!/^@[a-zA-Z0-9_-]*$/.test(find)) {
      throw new Error('Aliases must start with @');
    }
    aliases.push({ find, replacement: await packPath(root, value, true) });
  }
  const setup = manifest.setup === undefined ? null : await packPath(root, manifest.setup);
  return { root, templates, aliases, setup };
}

async function createCache(packRoot, cacheDirectory) {
  const cacheRoot = path.resolve(cacheDirectory);
  if (isInside(packRoot, cacheRoot)) {
    throw new Error('Cache must be outside template pack');
  }
  await mkdir(cacheRoot, { recursive: true });
  if (isInside(packRoot, await realpath(cacheRoot))) {
    throw new Error('Cache must be outside template pack');
  }
  return mkdtemp(path.join(cacheRoot, 'pack-'));
}

async function writeEntry(manifest, cache) {
  const imports = Object.values(manifest.templates).map((file, index) =>
    `import Template${index} from ${JSON.stringify(file)};`);
  const entries = Object.keys(manifest.templates).map((id, index) =>
    `  ${JSON.stringify(id)}: Template${index}`);
  const source = [
    manifest.setup ? `import ${JSON.stringify(manifest.setup)};` : '',
    'import { createRenderer } from "./template-runtime.js";',
    ...imports,
    'const templates = {',
    entries.join(',\n'),
    '};',
    'export const render = createRenderer(templates);',
  ].join('\n');
  const entry = path.join(cache, 'entry.mjs');
  await copyFile(runtimeFile, path.join(cache, 'template-runtime.js'));
  await writeFile(entry, source);
  return entry;
}

function packSourcePlugin(packRoot, cache) {
  return {
    name: 'pack-source-assets',
    enforce: 'pre',
    async transform(source, id) {
      const filename = id.split('?')[0];
      if (!isInside(packRoot, filename)) {
        return;
      }
      for (const reference of sourceAssets(source, filename)) {
        const originalUrl = new URL(reference.relative, pathToFileURL(filename));
        const relativePath = path.relative(packRoot, fileURLToPath(originalUrl));
        const asset = await packPath(packRoot, relativePath);
        const assetUrl = pathToFileURL(asset);
        assetUrl.search = originalUrl.search;
        assetUrl.hash = originalUrl.hash;
        const replacement = `new URL(${JSON.stringify(assetUrl.href)})`;
        source = source.slice(0, reference.start) + replacement + source.slice(reference.end);
      }
      return source;
    },
    async load(id) {
      const filename = id.split('?')[0];
      if (!path.isAbsolute(filename)) {
        return;
      }
      const resolved = await realpath(filename);
      const allowed = [packRoot, dependenciesRoot, cache].some(root => isInside(root, resolved));
      if (!allowed) {
        throw new Error('Import escapes template directory');
      }
    },
  };
}

function buildOptions(manifest, cache, entry) {
  // Resolve engine-owned packages even when the read-only pack is outside this project.
  const dependencies = ['vue', 'vue-router', 'moment', 'linq'].map(name => ({
    find: name,
    replacement: path.join(dependenciesRoot, name),
  }));
  return {
    configFile: false,
    root: manifest.root,
    logLevel: 'warn',
    resolve: {
      alias: [
        ...manifest.aliases,
        { find: /^moment$/, replacement: path.join(dependenciesRoot, 'moment/moment.js') },
        ...dependencies,
      ],
    },
    plugins: [
      packSourcePlugin(manifest.root, cache),
      vue({ template: { compilerOptions: { isCustomElement: tag => workbookTags.has(tag.toLowerCase()) } } }),
    ],
    ssr: { noExternal: true },
    build: {
      ssr: entry,
      outDir: path.join(cache, 'dist'),
      emptyOutDir: true,
      minify: false,
      rollupOptions: { output: { entryFileNames: 'render.mjs' } },
    },
  };
}

export async function compilePack(directory, cacheDirectory) {
  const manifest = await readManifest(directory);
  const cache = await createCache(manifest.root, cacheDirectory);
  const dispose = () => rm(cache, { recursive: true, force: true });
  try {
    const entry = await writeEntry(manifest, cache);
    await build(buildOptions(manifest, cache, entry));
    return {
      ids: Object.keys(manifest.templates),
      bundle: pathToFileURL(path.join(cache, 'dist/render.mjs')).href,
      dispose,
    };
  } catch (error) {
    await dispose();
    throw error;
  }
}
