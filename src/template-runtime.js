import { createSSRApp } from 'vue';
import { renderToString } from 'vue/server-renderer';

export function createRenderer(templates) {
  return async function render(id, data) {
    const app = createSSRApp(templates[id]);
    app.config.throwUnhandledErrorInProduction = true;

    // Vue SSR can swallow rejected async setup promises. Record handled errors
    // and reject the whole render instead of returning a partial workbook.
    let failed = false;
    let failure;
    app.config.errorHandler = error => {
      failed = true;
      failure = error;
    };

    app.provide('ssrRequest', { body: data });
    const xml = await renderToString(app, {});
    if (failed) {
      throw failure;
    }
    return xml;
  };
}
