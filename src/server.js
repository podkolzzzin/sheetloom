import Fastify from 'fastify';
import { RenderError, renderXml } from './render.js';

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);

function errorResponse(error) {
  const status = error.status ?? error.statusCode ?? 500;
  if (error instanceof RenderError) {
    return { status, code: error.code, message: error.message };
  }
  const code = status === 413 ? 'body_too_large' : status < 500 ? 'invalid_request' : 'internal_error';
  const message = status < 500 ? 'Invalid request' : 'Internal error';
  return { status, code, message };
}

export function createServer({ pack, bodyLimit = 10 * 1024 * 1024, timeoutMs = 120000, concurrency = 2 }) {
  const app = Fastify({
    bodyLimit,
    logger: false,
    requestTimeout: timeoutMs,
    connectionTimeout: 0,
    // Preserve arbitrary JSON keys. Data is passed to a worker, never merged into engine objects.
    onProtoPoisoning: 'ignore',
    onConstructorPoisoning: 'ignore',
    ajv: { customOptions: { removeAdditional: false, coerceTypes: false } },
  });
  const ids = new Set(pack.ids);
  let active = 0;

  app.setErrorHandler((error, request, reply) => {
    const { status, code, message } = errorResponse(error);
    reply.code(status).send({ error: { code, message } });
  });
  app.setNotFoundHandler((request, reply) => {
    reply.code(404).send({ error: { code: 'not_found', message: 'Route not found' } });
  });
  app.get('/health', async () => ({ status: 'ok' }));
  app.get('/templates', async () => ({ templates: pack.ids }));
  app.post('/render/:id', async (request, reply) => {
    if (!ids.has(request.params.id)) {
      throw new RenderError(404, 'unknown_template', 'Unknown template');
    }
    if (!isObject(request.body) || !Object.hasOwn(request.body, 'data') ||
        Object.keys(request.body).some(key => key !== 'data')) {
      throw new RenderError(400, 'invalid_request', 'Expected data');
    }
    if (active >= concurrency) {
      throw new RenderError(503, 'busy', 'Template capacity reached; retry later');
    }

    active++;
    const cancellation = new AbortController();
    const cancel = () => cancellation.abort();
    reply.raw.once('close', cancel);
    try {
      const xml = await renderXml(pack.bundle, request.params.id, request.body.data, timeoutMs, cancellation.signal);
      if (Buffer.byteLength(xml) > 8 * 1024 * 1024) {
        throw new RenderError(413, 'output_too_large', 'Rendered XML exceeds limit');
      }
      return reply.type('application/xml; charset=utf-8').send(xml);
    } finally {
      reply.raw.removeListener('close', cancel);
      active--;
    }
  });
  return app;
}
