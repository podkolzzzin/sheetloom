import { parse, babelParse } from 'vue/compiler-sfc';

function scriptBlocks(source, filename) {
  if (filename.endsWith('.vue')) {
    const { descriptor } = parse(source, { filename });
    return [descriptor.script, descriptor.scriptSetup]
      .filter(Boolean)
      .map(block => ({
        content: block.content,
        offset: block.loc.start.offset,
        language: block.lang ?? 'js',
      }));
  }
  if (/\.(?:[cm]?[jt]s|[jt]sx)$/.test(filename)) {
    return [{ content: source, offset: 0, language: filename.split('.').at(-1) }];
  }
  return [];
}

function relativeAssetPath(node) {
  if (node.type !== 'NewExpression' || node.callee?.type !== 'Identifier' ||
      node.callee.name !== 'URL' || node.arguments.length !== 2) {
    return null;
  }
  const [relative, base] = node.arguments;
  let value = null;
  if (relative?.type === 'StringLiteral') {
    value = relative.value;
  } else if (relative?.type === 'TemplateLiteral' && relative.expressions.length === 0) {
    value = relative.quasis[0].value.cooked;
  }
  const usesImportMetaUrl = base?.type === 'MemberExpression' && !base.computed &&
    base.property?.name === 'url' && base.object?.type === 'MetaProperty' &&
    base.object.meta.name === 'import' && base.object.property.name === 'meta';
  return usesImportMetaUrl && typeof value === 'string' && value.startsWith('.') ? value : null;
}

// Inspect executable script nodes only: examples in strings, comments and cells
// must never be mistaken for filesystem asset references.
export function sourceAssets(source, filename) {
  const references = [];
  for (const { content, offset, language } of scriptBlocks(source, filename)) {
    if (!content.includes('import.meta')) {
      continue;
    }
    const plugins = [];
    if (['ts', 'tsx', 'mts', 'cts'].includes(language)) {
      plugins.push('typescript');
    }
    if (['jsx', 'tsx'].includes(language)) {
      plugins.push('jsx');
    }
    const ast = babelParse(content, { sourceType: 'module', plugins });
    function visit(node) {
      if (!node || typeof node !== 'object') {
        return;
      }
      const relative = relativeAssetPath(node);
      if (relative !== null) {
        references.push({ start: node.start + offset, end: node.end + offset, relative });
      }
      for (const value of Object.values(node)) {
        if (Array.isArray(value)) {
          value.forEach(visit);
        } else if (value && typeof value === 'object' && value.type) {
          visit(value);
        }
      }
    }
    visit(ast);
  }
  return references.sort((a, b) => b.start - a.start);
}
