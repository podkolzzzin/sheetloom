import Fastify from 'fastify';
import { RenderError, renderXml } from './render.js';

const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
export function createServer({ pack, rendererUrl, bodyLimit = 10 * 1024 * 1024, timeoutMs = 120000, concurrency = 2 }) {
  const app = Fastify({ bodyLimit, logger: false, requestTimeout: timeoutMs, connectionTimeout: 0,
    ajv: { customOptions: { removeAdditional: false, coerceTypes: false } } });
  const ids = new Set(pack.ids);
  let active = 0;
  app.setErrorHandler((error, request, reply) => {
    const status = error.status ?? error.statusCode ?? 500;
    const code = error instanceof RenderError ? error.code : status === 413 ? 'body_too_large' : status < 500 ? 'invalid_request' : 'internal_error';
    const message = error instanceof RenderError ? error.message : status < 500 ? 'Invalid request' : 'Internal error';
    reply.code(status).send({ error: { code, message } });
  });
  app.setNotFoundHandler((request, reply) => reply.code(404).send({ error: { code: 'not_found', message: 'Route not found' } }));
  app.get('/health', async () => {
    try {
      const response = await fetch(`${rendererUrl}/health`, { signal: AbortSignal.timeout(2000) });
      await response.body?.cancel();
      if (!response.ok) throw new Error();
      return { status: 'ok' };
    } catch { throw new RenderError(503, 'renderer_unavailable', 'Renderer unavailable'); }
  });
  app.get('/v1/templates', async () => ({ templates: pack.ids }));
  app.post('/v1/render/:id', async (request, reply) => {
    if (!ids.has(request.params.id)) throw new RenderError(404, 'unknown_template', 'Unknown template');
    const body = request.body;
    if (!object(body) || !Object.hasOwn(body, 'data') || Object.keys(body).some(key => !['data', 'options'].includes(key)) ||
        (body.options !== undefined && (!object(body.options) || Object.keys(body.options).some(key => key !== 'precisionAsDisplayed') ||
          (body.options.precisionAsDisplayed !== undefined && typeof body.options.precisionAsDisplayed !== 'boolean')))) {
      throw new RenderError(400, 'invalid_request', 'Expected data and optional boolean options.precisionAsDisplayed');
    }
    if (active >= concurrency) throw new RenderError(503, 'busy', 'Render capacity reached; retry later');
    active++;
    const start = Date.now();
    try {
      const xml = await renderXml(pack.bundle, request.params.id, body.data, timeoutMs);
      if (Buffer.byteLength(xml) > 8 * 1024 * 1024) throw new RenderError(413, 'output_too_large', 'Rendered XML exceeds limit');
      let response;
      try {
        response = await fetch(`${rendererUrl}/render`, {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ xml, precisionAsDisplayed: body.options?.precisionAsDisplayed ?? false }),
          signal: AbortSignal.timeout(Math.max(1, timeoutMs - (Date.now() - start))),
        });
        if (!response.ok) {
          await response.body?.cancel();
          if ([400, 422].includes(response.status)) throw new RenderError(422, 'invalid_workbook', 'Template produced invalid workbook XML');
          if (response.status === 503) throw new RenderError(503, 'busy', 'Renderer capacity reached; retry later');
          if (response.status === 413) throw new RenderError(413, 'output_too_large', 'Rendered XML exceeds limit');
          throw new RenderError(502, 'renderer_failed', 'Renderer failed');
        }
        const output = Buffer.from(await response.arrayBuffer());
        if (output[0] !== 0x50 || output[1] !== 0x4b) throw new RenderError(502, 'renderer_failed', 'Renderer returned invalid XLSX');
        return reply.type('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
          .header('Content-Disposition', `attachment; filename="${request.params.id}.xlsx"`)
          .header('Cache-Control', 'no-store').send(output);
      } catch (error) {
        if (error instanceof RenderError) throw error;
        if (error.name === 'TimeoutError' || error.name === 'AbortError') throw new RenderError(504, 'render_timeout', 'Workbook rendering timed out');
        throw new RenderError(502, 'renderer_unavailable', 'Renderer unavailable');
      }
    } finally { active--; }
  });
  return app;
}
