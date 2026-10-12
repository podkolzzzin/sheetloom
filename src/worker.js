import { parentPort, workerData } from 'node:worker_threads';

try {
  const { render } = await import(workerData.bundle);
  const xml = await render(workerData.id, workerData.data);
  parentPort.postMessage({ xml });
} catch {
  parentPort.postMessage({ error: 'Template rendering failed' });
}
