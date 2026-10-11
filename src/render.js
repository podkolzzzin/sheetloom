import { Worker } from 'node:worker_threads';

export class RenderError extends Error {
  constructor(status, code, message) { super(message); this.status = status; this.code = code; }
}

export function renderXml(bundle, id, data, timeoutMs) {
  return new Promise((resolve, reject) => {
    const worker = new Worker(new URL('./worker.js', import.meta.url), {
      workerData: { bundle, id, data }, resourceLimits: { maxOldGenerationSizeMb: 512 },
      stdout: true, stderr: true,
    });
    // Drain template output without logging potentially sensitive request data.
    worker.stdout.resume(); worker.stderr.resume();
    let settled = false;
    const finish = (error, xml) => {
      if (settled) return;
      settled = true; clearTimeout(timer);
      worker.terminate().finally(() => error ? reject(error) : resolve(xml));
    };
    const timer = setTimeout(() => finish(new RenderError(504, 'render_timeout', 'Template rendering timed out')), timeoutMs);
    worker.once('message', value => value.error
      ? finish(new RenderError(422, 'template_failed', value.error)) : finish(null, value.xml));
    worker.once('error', () => finish(new RenderError(422, 'template_failed', 'Template rendering failed')));
    worker.once('exit', () => finish(new RenderError(422, 'template_failed', 'Template worker exited')));
  });
}
