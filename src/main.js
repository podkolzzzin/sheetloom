import { compilePack } from './pack.js';
import { createServer } from './server.js';

function integer(name, fallback, max) {
  const value = Number(process.env[name] ?? fallback);
  if (!Number.isInteger(value) || value < 1 || value > max) {
    throw new Error(`Invalid ${name}`);
  }
  return value;
}

let pack;
let app;
let stopping = false;

async function stop(code) {
  if (stopping) {
    return;
  }
  stopping = true;
  const forcedExit = setTimeout(() => process.exit(code || 1), 5000);
  try {
    await app?.close();
  } finally {
    await pack?.dispose();
    clearTimeout(forcedExit);
    process.exit(code);
  }
}

process.on('SIGTERM', () => void stop(0));
process.on('SIGINT', () => void stop(0));

try {
  const port = integer('NODE_PORT', 8081, 65535);
  pack = await compilePack(
    process.env.TEMPLATES_DIR ?? '/templates',
    process.env.CACHE_DIR ?? '/tmp/sheetloom',
  );
  app = createServer({
    pack,
    bodyLimit: integer('BODY_LIMIT_BYTES', 10485760, 67108864),
    timeoutMs: integer('RENDER_TIMEOUT_MS', 120000, 600000),
    concurrency: integer('RENDER_CONCURRENCY', 2, 32),
  });
  await app.listen({ port, host: '127.0.0.1' });
  console.info(JSON.stringify({ event: 'templates_ready', port, templates: pack.ids.length }));
} catch (error) {
  console.error(`Template service startup failed: ${error.message}`);
  await stop(1);
}
