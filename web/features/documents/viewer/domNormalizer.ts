import type { CSSProperties } from 'react';
import type { ChunkSourceMap, DocumentAsset, DocumentNode, DocumentOutlineItem, DocumentParserInput, StructuredNode } from '@/features/documents/viewer/types';
import { DOCUMENT_PARSER_VERSION, annotateTextRanges, createIdFactory, createDocumentMetadata, extractPlainText, normalizeTextForMatching } from '@/features/documents/viewer/utils';

type DomNormalizationOptions = {
  input: DocumentParserInput;
  root: HTMLElement;
  format: 'docx' | 'xlsx' | 'html';
  outlineKind: 'section' | 'sheet';
  limitations?: string[];
};

type NormalizationState = {
  createId: ReturnType<typeof createIdFactory>;
  outline: DocumentOutlineItem[];
  assets: DocumentAsset[];
  outlineIndex: number;
};

const NON_RENDERABLE_TAGS = new Set([
  'style',
  'script',
  'noscript',
  'template',
  'meta',
  'link',
  'title',
  'head'
]);

export function normalizeDomDocument(options: DomNormalizationOptions) {
  const state: NormalizationState = {
    createId: createIdFactory(`${options.input.documentId}-${options.format}`),
    outline: [],
    assets: [],
    outlineIndex: 0
  };

  const nodes = normalizeChildNodes(Array.from(options.root.childNodes), state, {
    sourceType: options.outlineKind,
    parserVersion: DOCUMENT_PARSER_VERSION
  });
  const annotatedRoot = annotateTextRanges(nodes);
  const plainText = extractPlainText(annotatedRoot);

  return {
    model: {
      metadata: createDocumentMetadata({
        documentId: options.input.documentId,
        title: options.input.title,
        originalFileName: options.input.originalFileName,
        contentType: options.input.contentType,
        format: options.format,
        version: options.input.version,
        status: options.input.status,
        createdAtUtc: options.input.createdAtUtc,
        updatedAtUtc: options.input.updatedAtUtc,
        warnings: [],
        limitations: options.limitations ?? []
      }),
      root: annotatedRoot,
      outline: state.outline,
      assets: state.assets,
      plainText,
      normalizedText: normalizeTextForMatching(plainText)
    },
    parserWarnings: []
  };
}

function normalizeChildNodes(nodes: ChildNode[], state: NormalizationState, inheritedSourceMap: ChunkSourceMap): DocumentNode[] {
  return nodes.flatMap((node) => normalizeNode(node, state, inheritedSourceMap)).filter(isMeaningfulNode);
}

function normalizeTextNode(node: ChildNode, state: NormalizationState, inheritedSourceMap: ChunkSourceMap): DocumentNode[] {
  const text = node.textContent?.replaceAll(/\r\n?/g, '\n') ?? '';
  if (!text.trim() && !text.includes('\n')) {
    return [];
  }

  return [{
    id: state.createId('text'),
    kind: 'text',
    text,
    style: {
      css: {
        whiteSpace: text.includes('\n') ? 'pre-wrap' : undefined
      },
      preserveWhitespace: text.includes('\n')
    },
    sourceMap: inheritedSourceMap
  }];
}

function normalizeNode(node: ChildNode, state: NormalizationState, inheritedSourceMap: ChunkSourceMap): DocumentNode[] {
  if (node.nodeType === Node.TEXT_NODE) {
    return normalizeTextNode(node, state, inheritedSourceMap);
  }

  if (node.nodeType !== Node.ELEMENT_NODE) {
    return [];
  }

  const element = node as HTMLElement;
  const tagName = element.tagName.toLowerCase();
  const sourceMap = readElementSourceMap(element, inheritedSourceMap);

  if (NON_RENDERABLE_TAGS.has(tagName)) {
    return [];
  }

  if (tagName === 'br') {
    return [{ id: state.createId('br'), kind: 'line-break', sourceMap }];
  }

  if (tagName === 'img') {
    const imageId = state.createId('image');
    const src = element.getAttribute('src') ?? '';
    if (src) {
      state.assets.push({
        assetId: imageId,
        kind: 'image',
        mimeType: guessImageMimeType(src),
        src,
        name: element.getAttribute('alt') ?? undefined,
        sourceNodeId: imageId
      });
    }

    return [{
      id: imageId,
      kind: 'image',
      src,
      alt: element.getAttribute('alt') ?? undefined,
      width: readNumeric(element, 'width'),
      height: readNumeric(element, 'height'),
      style: { css: pickCssStyle(globalThis.getComputedStyle(element)) },
      sourceMap: {
        ...sourceMap,
        sourceType: 'image'
      }
    }];
  }

  const children = normalizeChildNodes(Array.from(element.childNodes), state, sourceMap);
  if (children.length === 0 && !['td', 'th', 'tr', 'table'].includes(tagName)) {
    return [];
  }

  const nodeId = state.createId(tagName);
  const computedStyle = globalThis.getComputedStyle(element);
  const structured = createStructuredNode(nodeId, tagName, children, computedStyle, sourceMap);
  if (structured) {
    maybeRegisterOutline(structured, element, state);
  }

  return structured ? [structured] : children;
}

function createStructuredNode(
  nodeId: string,
  tagName: string,
  children: DocumentNode[],
  computedStyle: CSSStyleDeclaration,
  sourceMap: ChunkSourceMap
): StructuredNode | null {
  const base = {
    id: nodeId,
    children,
    style: {
      css: pickCssStyle(computedStyle),
      preserveWhitespace: computedStyle.whiteSpace.includes('pre')
    },
    sourceMap
  } as const;

  switch (tagName) {
    case 'article':
    case 'section':
      return { ...base, kind: 'section', label: sourceMap.sectionTitle };
    case 'p':
      return { ...base, kind: 'paragraph' };
    case 'h1':
    case 'h2':
    case 'h3':
    case 'h4':
    case 'h5':
    case 'h6':
      return { ...base, kind: 'heading', level: Number(tagName[1]) };
    case 'ul':
      return { ...base, kind: 'list', ordered: false };
    case 'ol':
      return { ...base, kind: 'list', ordered: true };
    case 'li':
      return { ...base, kind: 'list-item' };
    case 'table':
      return { ...base, kind: 'table' };
    case 'thead':
    case 'tbody':
    case 'tfoot':
    case 'div':
    case 'main':
    case 'body':
    case 'header':
    case 'footer':
      return { ...base, kind: 'container' };
    case 'tr':
      return { ...base, kind: 'table-row' };
    case 'th':
    case 'td':
      return { ...base, kind: 'table-cell' };
    case 'a':
      return {
        ...base,
        kind: 'link',
        metadata: {
          href: (sourceMap as ChunkSourceMap & { href?: string }).href ?? ''
        }
      };
    default:
      return { ...base, kind: 'container' };
  }
}

function maybeRegisterOutline(node: StructuredNode, element: HTMLElement, state: NormalizationState) {
  const explicitLabel = element.dataset.outlineLabel?.trim();
  const textLabel = element.textContent?.trim();
  const label = explicitLabel || (node.kind === 'heading' ? textLabel : undefined);
  const outlineKind = element.dataset.outlineKind as DocumentOutlineItem['kind'] | undefined;

  if (!label || (!outlineKind && node.kind !== 'heading' && node.kind !== 'section' && node.kind !== 'sheet')) {
    return;
  }

  let inferredKind: DocumentOutlineItem['kind'] = 'section';
  if (node.kind === 'sheet') {
    inferredKind = 'sheet';
  }

  const kind = outlineKind ?? inferredKind;
  state.outlineIndex += 1;
  state.outline.push({
    id: element.dataset.outlineId ?? `${node.id}-outline`,
    label,
    kind,
    index: state.outlineIndex,
    targetNodeId: node.id,
    pageNumber: node.sourceMap?.pageNumber,
    sectionTitle: label,
    sheetName: node.sourceMap?.sheetName,
    slideNumber: node.sourceMap?.slideNumber
  });
}

function readElementSourceMap(element: HTMLElement, inherited: ChunkSourceMap): ChunkSourceMap {
  const sourceType = (element.dataset.sourceType as ChunkSourceMap['sourceType'] | undefined) ?? inherited.sourceType;
  const pageNumber = readOptionalNumber(element.dataset.pageNumber) ?? inherited.pageNumber;
  const endPageNumber = readOptionalNumber(element.dataset.endPageNumber) ?? inherited.endPageNumber;
  const slideNumber = readOptionalNumber(element.dataset.slideNumber) ?? inherited.slideNumber;
  const href = element.getAttribute('href');

  return {
    ...inherited,
    sourceType,
    pageNumber,
    endPageNumber,
    slideNumber,
    sectionId: element.dataset.sectionId ?? inherited.sectionId,
    sectionTitle: element.dataset.sectionTitle ?? inherited.sectionTitle,
    sheetName: element.dataset.sheetName ?? inherited.sheetName,
    headingPath: inherited.headingPath,
    parserVersion: DOCUMENT_PARSER_VERSION,
    ...(href ? ({ href } as Record<string, string>) : {})
  };
}

function pickCssStyle(computedStyle: CSSStyleDeclaration): Partial<CSSProperties> {
  const backgroundColor = selectCssValue(computedStyle.backgroundColor, ['rgba(0, 0, 0, 0)']);
  const border = selectCssValue(computedStyle.border, ['0px none rgb(0, 0, 0)']);
  const padding = selectCssValue(computedStyle.padding, ['0px']);
  const margin = selectCssValue(computedStyle.margin, ['0px']);
  const width = selectCssValue(computedStyle.width, ['auto']);
  const maxWidth = selectCssValue(computedStyle.maxWidth, ['none']);

  return {
    color: computedStyle.color,
    backgroundColor,
    fontFamily: computedStyle.fontFamily,
    fontSize: computedStyle.fontSize,
    fontWeight: computedStyle.fontWeight,
    fontStyle: computedStyle.fontStyle,
    textDecorationLine: computedStyle.textDecorationLine,
    textAlign: computedStyle.textAlign as CSSProperties['textAlign'],
    whiteSpace: computedStyle.whiteSpace as CSSProperties['whiteSpace'],
    lineHeight: computedStyle.lineHeight,
    listStyleType: computedStyle.listStyleType,
    borderCollapse: computedStyle.borderCollapse as CSSProperties['borderCollapse'],
    border,
    padding,
    margin,
    width,
    maxWidth
  };
}

function selectCssValue(value: string, ignoredValues: string[]) {
  return ignoredValues.includes(value) ? undefined : value;
}

function guessImageMimeType(src: string) {
  if (src.startsWith('data:image/')) {
    return src.slice(5, src.indexOf(';'));
  }

  if (src.endsWith('.png')) {
    return 'image/png';
  }

  if (src.endsWith('.jpg') || src.endsWith('.jpeg')) {
    return 'image/jpeg';
  }

  if (src.endsWith('.gif')) {
    return 'image/gif';
  }

  return 'image/*';
}

function readNumeric(element: HTMLElement, attribute: string) {
  const value = element.getAttribute(attribute);
  return value ? Number(value) : undefined;
}

function readOptionalNumber(value: string | undefined) {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function isMeaningfulNode(node: DocumentNode) {
  return node.kind !== 'text' || node.text.trim().length > 0 || node.text.includes('\n');
}