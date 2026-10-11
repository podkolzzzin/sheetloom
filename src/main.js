import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import { compilePack } from './pack.js';
import { createServer } from './server.js';

function integer(name, fallback, max) {
  const value = Number(process.env[name] ?? fallback);
  if (!Number.isInteger(value) || value < 1 || value > max) throw new Error(`Invalid ${name}`);
  return value;
}

let renderer, pack, app, stopping = false;
async function stop(code) {
  if (stopping) return;
  stopping = true;
  const forcedExit = setTimeout(() => { renderer?.kill('SIGKILL'); process.exit(code || 1); }, 10000);
  try { await app?.close(); } finally {
    if (renderer && renderer.exitCode === null && renderer.signalCode === null) {
      await new Promise(resolve => { renderer.once('exit', resolve); renderer.kill('SIGTERM'); });
    }
    await pack?.dispose();
    clearTimeout(forcedExit);
    process.exit(code);
  }
}
process.on('SIGTERM', () => void stop(0));
process.on('SIGINT', () => void stop(0));
try {
  const port = integer('PORT', 8080, 65535);
  const rendererPort = integer('RENDERER_PORT', 8081, 65535);
  const rendererUrl = `http://127.0.0.1:${rendererPort}`;
  renderer = spawn('dotnet', [process.env.RENDERER_DLL ?? '/app/renderer/ExcelTemplateEngine.Service.dll'], { stdio: 'inherit' });
  renderer.once('error', () => { console.error('Renderer process could not start'); void stop(1); });
  renderer.once('exit', () => { if (!stopping) { console.error('Renderer process exited'); void stop(1); } });
  pack = await compilePack(process.env.TEMPLATES_DIR ?? '/templates', process.env.CACHE_DIR ?? '/tmp/excel-template-engine');
  let ready = false;
  for (let attempt = 0; attempt < 120; attempt++) {
    try {
      const response = await fetch(`${rendererUrl}/health`, { signal: AbortSignal.timeout(1000) });
      ready = response.ok; await response.body?.cancel();
      if (ready) break;
    } catch { /* Renderer is still starting. */ }
    await delay(250);
  }
  if (!ready) throw new Error('Renderer startup timeout');
  app = createServer({ pack, rendererUrl, bodyLimit: integer('BODY_LIMIT_BYTES', 10485760, 67108864),
    timeoutMs: integer('RENDER_TIMEOUT_MS', 120000, 600000), concurrency: integer('RENDER_CONCURRENCY', 2, 32) });
  await app.listen({ port, host: '0.0.0.0' });
  console.info(JSON.stringify({ event: 'ready', port, templates: pack.ids.length }));
} catch (error) {
  console.error(`Startup failed: ${error.message}`);
  await stop(1);
}
