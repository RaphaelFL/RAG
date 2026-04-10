import type { UnifiedDocumentModel, ViewerChunk } from '@/features/documents/viewer/types';

export function downloadBlob(fileName: string, blob: Blob) {
  const objectUrl = globalThis.URL.createObjectURL(blob);
  const link = globalThis.document.createElement('a');
  link.href = objectUrl;
  link.download = fileName;
  globalThis.document.body.append(link);
  link.click();
  link.remove();
  globalThis.setTimeout(() => globalThis.URL.revokeObjectURL(objectUrl), 0);
}

export function downloadText(fileName: string, content: string, type = 'text/plain;charset=utf-8') {
  downloadBlob(fileName, new Blob([content], { type }));
}

export function exportChunkAsHtml(chunk: ViewerChunk) {
  const html = [
    '<!doctype html>',
    '<html lang="pt-BR">',
    '<head>',
    '<meta charset="utf-8" />',
    `<title>${escapeHtml(chunk.chunkId)}</title>`,
    '<meta name="viewport" content="width=device-width, initial-scale=1" />',
    '</head>',
    '<body>',
    chunk.formattedHtml ?? '',
    '</body>',
    '</html>'
  ].join('');

  downloadText(sanitizeFileName(`${chunk.referenceFileName}-${chunk.chunkId}.html`), html, 'text/html;charset=utf-8');
}

export function exportChunkAsJson(chunk: ViewerChunk) {
  downloadText(
    sanitizeFileName(`${chunk.referenceFileName}-${chunk.chunkId}.json`),
    JSON.stringify(chunk, null, 2),
    'application/json;charset=utf-8'
  );
}

export function exportChunksAsJson(document: UnifiedDocumentModel, chunks: ViewerChunk[]) {
  const payload = {
    document: {
      documentId: document.metadata.documentId,
      title: document.metadata.title,
      originalFileName: document.metadata.originalFileName,
      contentType: document.metadata.contentType,
      parserVersion: document.metadata.parserVersion,
      format: document.metadata.format,
      warnings: document.metadata.warnings,
      capabilities: document.metadata.capabilities
    },
    exportedAtUtc: new Date().toISOString(),
    chunks
  };

  downloadText(
    sanitizeFileName(`${document.metadata.originalFileName}-chunks.json`),
    JSON.stringify(payload, null, 2),
    'application/json;charset=utf-8'
  );
}

export function exportChunksAsHtmlArchive(document: UnifiedDocumentModel, chunks: ViewerChunk[]) {
  const html = [
    '<!doctype html>',
    '<html lang="pt-BR">',
    '<head>',
    '<meta charset="utf-8" />',
    `<title>${escapeHtml(document.metadata.title)}</title>`,
    '<meta name="viewport" content="width=device-width, initial-scale=1" />',
    '<style>body{font-family:Georgia,serif;padding:24px}article{border:1px solid #ddd;border-radius:16px;padding:20px;margin:0 0 20px}header{margin:0 0 16px}small{color:#555}</style>',
    '</head>',
    '<body>',
    `<h1>${escapeHtml(document.metadata.title)}</h1>`,
    ...chunks.map((chunk) => [
      '<article>',
      `<header><h2>${escapeHtml(chunk.chunkId)}</h2><small>${escapeHtml(chunk.originLabel)}</small></header>`,
      chunk.formattedHtml ?? '',
      '</article>'
    ].join('')),
    '</body>',
    '</html>'
  ].join('');

  downloadText(
    sanitizeFileName(`${document.metadata.originalFileName}-chunks.html`),
    html,
    'text/html;charset=utf-8'
  );
}

function escapeHtml(value: string) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function sanitizeFileName(value: string) {
  return value.replaceAll(/[<>:"/\\|?*\u0000-\u001F]/g, '-');
}