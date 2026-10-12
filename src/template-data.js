import { hasInjectionContext, inject } from 'vue';

/**
 * Read this render's data during Vue component setup.
 * @template T
 * @returns {T}
 */
export function getTemplateData() {
  if (!hasInjectionContext()) {
    throw new Error('getTemplateData() must be called during Vue component setup');
  }

  const data = inject('templateData', undefined);
  if (data === undefined) {
    throw new Error('Template data was not provided by Sheetloom');
  }
  return /** @type {T} */ (data);
}
