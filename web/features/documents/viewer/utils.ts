import type { DocumentNode, ChunkSourceMap, ChunkStyle, DocumentMetadataModel, SupportedDocumentFormat, UnifiedDocumentModel, ViewerChunk } from '@/features/documents/viewer/types';

export const DOCUMENT_PARSER_VERSION = 'viewer-1.0.0';

export function createIdFactory(prefix: string) {
  let index = 0;
  return (suffix?: string) => `${prefix}-${suffix ?? 'node'}-${++index}`;
}

export function detectDocumentFormat(fileName: string, contentType: string): SupportedDocumentFormat {
  const normalizedName = fileName.trim().toLowerCase();
  const normalizedType = contentType.trim().toLowerCase();

  if (normalizedType.startsWith('image/') || /\.(png|jpe?g|gif|webp|bmp|svg)$/i.test(normalizedName)) {
    return 'image';
  }

  if (normalizedType === 'application/pdf' || normalizedName.endsWith('.pdf')) {
    return 'pdf';
  }

  if (normalizedType === 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' || normalizedName.endsWith('.docx')) {
    return 'docx';
  }

  if (normalizedType === 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' || normalizedName.endsWith('.xlsx')) {
    return 'xlsx';
  }

  if (normalizedType === 'application/vnd.openxmlformats-officedocument.presentationml.presentation' || normalizedName.endsWith('.pptx')) {
    return 'pptx';
  }

  if (normalizedType.includes('html') || /\.(html?|xhtml)$/i.test(normalizedName)) {
    return 'html';
  }

  if (normalizedType.startsWith('text/') || /\.(txt|md|csv|log)$/i.test(normalizedName)) {
    return 'txt';
  }

  return 'unknown';
}

export function normalizeTextForMatching(value: string) {
  return value
    .replaceAll(/\r\n?/g, '\n')
    .replaceAll(/\u00a0/g, ' ')
    .replaceAll(/[\t\f\v ]+/g, ' ')
    .replaceAll(/\n{3,}/g, '\n\n')
    .trim();
}

export function buildNormalizedIndex(value: string) {
  const originalOffsets: number[] = [];
  let normalized = '';
  let lastWasWhitespace = false;

  for (let index = 0; index < value.length; index += 1) {
    const current = value[index];
    const normalizedChar = current === '\r' ? '' : current === '\n' || /\s/.test(current) ? ' ' : current;
    if (!normalizedChar) {
      continue;
    }

    if (normalizedChar === ' ') {
      if (lastWasWhitespace) {
        continue;
      }

      lastWasWhitespace = true;
    } else {
      lastWasWhitespace = false;
    }

    normalized += normalizedChar;
    originalOffsets.push(index);
  }

  return {
    normalized: normalized.trim(),
    originalOffsets
  };
}

export function computeStableHash(value: string) {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return `fnv1a-${(hash >>> 0).toString(16).padStart(8, '0')}`;
}

export function escapeHtml(value: string) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function renderNodesToHtml(nodes: DocumentNode[]): string {
  return nodes.map((node) => renderNodeToHtml(node)).join('');
}

function renderNodeToHtml(node: DocumentNode): string {
  const style = styleToHtml(node.style);
  switch (node.kind) {
    case 'text':
      return `<span${style}>${escapeHtml(node.text)}</span>`;
    case 'line-break':
      return '<br />';
    case 'image':
      return `<figure${style}><img src="${escapeHtml(node.src)}" alt="${escapeHtml(node.alt ?? '')}" />${node.caption ? `<figcaption>${escapeHtml(node.caption)}</figcaption>` : ''}</figure>`;
    case 'heading': {
      const level = Math.min(Math.max(node.level ?? 2, 1), 6);
      return `<h${level}${style}>${node.children.map(renderNodeToHtml).join('')}</h${level}>`;
    }
    case 'paragraph':
      return `<p${style}>${node.children.map(renderNodeToHtml).join('')}</p>`;
    case 'list':
      return node.ordered
        ? `<ol${style}>${node.children.map(renderNodeToHtml).join('')}</ol>`
        : `<ul${style}>${node.children.map(renderNodeToHtml).join('')}</ul>`;
    case 'list-item':
      return `<li${style}>${node.children.map(renderNodeToHtml).join('')}</li>`;
    case 'table':
      return `<table${style}><tbody>${node.children.map(renderNodeToHtml).join('')}</tbody></table>`;
    case 'table-row':
      return `<tr${style}>${node.children.map(renderNodeToHtml).join('')}</tr>`;
    case 'table-cell':
      return `<td${style}>${node.children.map(renderNodeToHtml).join('')}</td>`;
    case 'link': {
      const href = node.metadata?.href ?? '#';
      return `<a href="${escapeHtml(href)}"${style}>${node.children.map(renderNodeToHtml).join('')}</a>`;
    }
    case 'document':
    case 'container':
    case 'page':
    case 'section':
    case 'sheet':
    case 'slide':
    default:
      return `<div${style}>${'children' in node ? node.children.map(renderNodeToHtml).join('') : ''}</div>`;
  }
}

function styleToHtml(style?: ChunkStyle) {
  if (!style?.css) {
    return '';
  }

  const css = Object.entries(style.css)
    .filter((entry): entry is [string, string | number] => entry[1] !== undefined && entry[1] !== null && entry[1] !== '')
    .map(([key, value]) => `${camelToKebab(key)}:${String(value)}`)
    .join(';');

  return css ? ` style="${escapeHtml(css)}"` : '';
}

function camelToKebab(value: string) {
  return value.replaceAll(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);
}

export function extractPlainText(nodes: DocumentNode[]): string {
  return nodes.map((node) => extractNodeText(node)).join('').trim();
}

export function extractNodeText(node: DocumentNode): string {
  switch (node.kind) {
    case 'text':
      return node.text;
    case 'line-break':
      return '\n';
    case 'image':
      return node.alt?.trim() ? `${node.alt}\n` : '';
    case 'table-cell':
      return `${node.children.map(extractNodeText).join(' ').trim()}\t`;
    case 'table-row':
      return `${node.children.map(extractNodeText).join('').trimEnd()}\n`;
    case 'table':
    case 'list':
    case 'page':
    case 'section':
    case 'sheet':
    case 'slide':
    case 'container':
    case 'document':
      return `${node.children.map(extractNodeText).join('')}\n`;
    case 'paragraph':
    case 'heading':
    case 'list-item':
    case 'link':
      return `${node.children.map(extractNodeText).join('')}\n`;
    default:
      return '';
  }
}

export function annotateTextRanges(nodes: DocumentNode[]) {
  let cursor = 0;

  function visit(node: DocumentNode): DocumentNode {
    if (node.kind === 'text') {
      const startOffset = cursor;
      cursor += node.text.length;
      return {
        ...node,
        sourceMap: {
          ...(node.sourceMap ?? { sourceType: 'range' }),
          startOffset,
          endOffset: cursor,
          parserVersion: node.sourceMap?.parserVersion ?? DOCUMENT_PARSER_VERSION
        }
      };
    }

    if (node.kind === 'line-break') {
      const startOffset = cursor;
      cursor += 1;
      return {
        ...node,
        sourceMap: {
          ...(node.sourceMap ?? { sourceType: 'range' }),
          startOffset,
          endOffset: cursor,
          parserVersion: node.sourceMap?.parserVersion ?? DOCUMENT_PARSER_VERSION
        }
      };
    }

    if (node.kind === 'image') {
      return {
        ...node,
        sourceMap: {
          ...(node.sourceMap ?? { sourceType: 'image' }),
          startOffset: cursor,
          endOffset: cursor,
          parserVersion: node.sourceMap?.parserVersion ?? DOCUMENT_PARSER_VERSION
        }
      };
    }

    const startOffset = cursor;
    const children = node.children.map((child) => visit(child));
    if (['paragraph', 'heading', 'list-item', 'table-row', 'page', 'section', 'sheet', 'slide'].includes(node.kind)) {
      cursor += 1;
    }

    return {
      ...node,
      children,
      sourceMap: {
        ...(node.sourceMap ?? { sourceType: inferSourceType(node.kind) }),
        startOffset,
        endOffset: cursor,
        parserVersion: node.sourceMap?.parserVersion ?? DOCUMENT_PARSER_VERSION
      }
    };
  }

  return nodes.map((node) => visit(node));
}

function inferSourceType(kind: DocumentNode['kind']): ChunkSourceMap['sourceType'] {
  switch (kind) {
    case 'page':
      return 'page';
    case 'section':
      return 'section';
    case 'sheet':
      return 'sheet';
    case 'slide':
      return 'slide';
    case 'table':
    case 'table-row':
    case 'table-cell':
      return 'table';
    case 'heading':
      return 'heading';
    case 'paragraph':
      return 'paragraph';
    case 'image':
      return 'image';
    default:
      return 'range';
  }
}

export function createDocumentMetadata(base: Omit<DocumentMetadataModel, 'parserVersion' | 'warnings' | 'capabilities' | 'format'> & {
  format: SupportedDocumentFormat;
  warnings?: string[];
  limitations?: string[];
  supportsFidelity?: boolean;
  supportsOriginalView?: boolean;
}): DocumentMetadataModel {
  return {
    ...base,
    parserVersion: DOCUMENT_PARSER_VERSION,
    warnings: base.warnings ?? [],
    capabilities: {
      supportsFidelity: base.supportsFidelity ?? true,
      supportsOriginalView: base.supportsOriginalView ?? true,
      limitations: base.limitations ?? []
    }
  };
}

export function buildOriginLabel(sourceMap: ChunkSourceMap) {
  if (sourceMap.sheetName) {
    return `Aba ${sourceMap.sheetName}`;
  }

  if (sourceMap.slideNumber) {
    return `Slide ${sourceMap.slideNumber}`;
  }

  if (sourceMap.pageNumber && sourceMap.endPageNumber && sourceMap.endPageNumber !== sourceMap.pageNumber) {
    return `Paginas ${sourceMap.pageNumber}-${sourceMap.endPageNumber}`;
  }

  if (sourceMap.pageNumber) {
    return `Pagina ${sourceMap.pageNumber}`;
  }

  if (sourceMap.sectionTitle) {
    return sourceMap.sectionTitle;
  }

  return 'Documento';
}

export function collectNodeIds(nodes: DocumentNode[]): string[] {
  const result: string[] = [];

  function visit(node: DocumentNode) {
    result.push(node.id);
    if ('children' in node) {
      node.children.forEach(visit);
    }
  }

  nodes.forEach(visit);
  return result;
}

export function createFallbackChunk(chunk: {
  chunkId: string;
  documentId: string;
  chunkIndex: number;
  rawText: string;
  referenceFileName: string;
  metadata: Record<string, string>;
  sourceMap: ChunkSourceMap;
  headingAncestry?: string[];
}): ViewerChunk {
  const textNode: DocumentNode = {
    id: `${chunk.chunkId}-text`,
    kind: 'text',
    text: chunk.rawText,
    style: {
      css: {
        whiteSpace: 'pre-wrap'
      },
      preserveWhitespace: true
    },
    sourceMap: chunk.sourceMap
  };

  const paragraph: DocumentNode = {
    id: `${chunk.chunkId}-paragraph`,
    kind: 'paragraph',
    children: [textNode],
    sourceMap: chunk.sourceMap
  };

  return {
    chunkId: chunk.chunkId,
    documentId: chunk.documentId,
    chunkIndex: chunk.chunkIndex,
    strategyId: 'backend-synced',
    sourceType: chunk.sourceMap.sourceType,
    rawText: chunk.rawText,
    normalizedText: normalizeTextForMatching(chunk.rawText),
    formattedContent: [paragraph],
    formattedHtml: renderNodesToHtml([paragraph]),
    contentHash: computeStableHash(chunk.rawText),
    referenceFileName: chunk.referenceFileName,
    parserVersion: DOCUMENT_PARSER_VERSION,
    originLabel: buildOriginLabel(chunk.sourceMap),
    headingAncestry: chunk.headingAncestry ?? [],
    sourceMap: chunk.sourceMap,
    metadata: chunk.metadata,
    fragments: [{ fragmentId: `${chunk.chunkId}-fragment`, nodeIds: [paragraph.id, textNode.id], sourceMap: chunk.sourceMap }],
    sourceNodeIds: [paragraph.id, textNode.id]
  };
}

export function buildViewerWarnings(document: UnifiedDocumentModel, chunks: ViewerChunk[]) {
  const warnings = [...document.metadata.warnings];
  if (chunks.length === 0) {
    warnings.push('Nenhum chunk estrutural foi gerado a partir do arquivo original.');
  }

  return Array.from(new Set(warnings));
}