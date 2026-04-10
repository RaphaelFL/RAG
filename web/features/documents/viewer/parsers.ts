import type { DocumentNode, DocumentOutlineItem, DocumentParserInput, ParsedDocumentPayload } from '@/features/documents/viewer/types';
import { normalizeDomDocument } from '@/features/documents/viewer/domNormalizer';
import { DOCUMENT_PARSER_VERSION, annotateTextRanges, createDocumentMetadata, createIdFactory, detectDocumentFormat, extractPlainText, normalizeTextForMatching } from '@/features/documents/viewer/utils';

const EMU_TO_PX = 9525;

export async function parseDocumentBlob(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const format = detectDocumentFormat(input.originalFileName, input.contentType);

  switch (format) {
    case 'pdf':
      return parsePdfDocument(input);
    case 'docx':
      return parseDocxDocument(input);
    case 'xlsx':
      return parseXlsxDocument(input);
    case 'pptx':
      return parsePptxDocument(input);
    case 'html':
      return parseHtmlDocument(input);
    case 'txt':
      return parseTextDocument(input);
    case 'image':
      return parseImageDocument(input);
    case 'unknown':
    default:
      return parseUnknownDocument(input);
  }
}

async function parseTextDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const createId = createIdFactory(`${input.documentId}-txt`);
  const content = await input.blob.text();
  const paragraphs = content.replaceAll(/\r\n?/g, '\n').split(/\n{2,}/g);
  const root: DocumentNode[] = [
    {
      id: createId('page'),
      kind: 'page',
      label: 'Pagina 1',
      sourceMap: {
        sourceType: 'page',
        pageNumber: 1,
        parserVersion: DOCUMENT_PARSER_VERSION
      },
      children: paragraphs.flatMap((paragraph, index) => {
        const lines = paragraph.split('\n');
        const paragraphNode: DocumentNode = {
          id: createId(`paragraph-${index + 1}`),
          kind: 'paragraph',
          style: {
            css: {
              whiteSpace: 'pre-wrap',
              fontFamily: 'var(--font-body)'
            },
            preserveWhitespace: true
          },
          sourceMap: {
            sourceType: 'paragraph',
            pageNumber: 1,
            parserVersion: DOCUMENT_PARSER_VERSION
          },
          children: lines.flatMap((line, lineIndex) => {
            const children: DocumentNode[] = [
              {
                id: createId(`text-${index + 1}-${lineIndex + 1}`),
                kind: 'text',
                text: line,
                style: {
                  css: {
                    whiteSpace: 'pre-wrap'
                  },
                  preserveWhitespace: true
                },
                sourceMap: {
                  sourceType: 'paragraph',
                  pageNumber: 1,
                  parserVersion: DOCUMENT_PARSER_VERSION
                }
              }
            ];
            if (lineIndex < lines.length - 1) {
              children.push({ id: createId('br'), kind: 'line-break', sourceMap: { sourceType: 'paragraph', pageNumber: 1, parserVersion: DOCUMENT_PARSER_VERSION } });
            }

            return children;
          })
        };

        return [paragraphNode];
      })
    }
  ];

  const annotatedRoot = annotateTextRanges(root);
  const plainText = extractPlainText(annotatedRoot);

  return {
    model: {
      metadata: createDocumentMetadata({
        documentId: input.documentId,
        title: input.title,
        originalFileName: input.originalFileName,
        contentType: input.contentType,
        format: 'txt',
        version: input.version,
        status: input.status,
        createdAtUtc: input.createdAtUtc,
        updatedAtUtc: input.updatedAtUtc,
        limitations: []
      }),
      root: annotatedRoot,
      outline: [{ id: `${input.documentId}-page-1`, label: 'Pagina 1', kind: 'page', index: 1, targetNodeId: root[0].id, pageNumber: 1 }],
      assets: [],
      plainText,
      normalizedText: normalizeTextForMatching(plainText)
    },
    parserWarnings: []
  };
}

async function parseHtmlDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const html = await input.blob.text();
  return withOffscreenContainer((container) => {
    const parser = new DOMParser();
    const document = parser.parseFromString(html, 'text/html');
    container.append(...Array.from(document.body.childNodes).map((node) => node.cloneNode(true)));
    return normalizeDomDocument({
      input,
      root: container,
      format: 'html',
      outlineKind: 'section',
      limitations: []
    });
  });
}

async function parseDocxDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const { renderAsync } = await import('docx-preview');
  const buffer = await input.blob.arrayBuffer();

  return withOffscreenContainer(async (container) => {
    await renderAsync(buffer, container, undefined, {
      breakPages: true,
      useBase64URL: true,
      renderChanges: false,
      renderFooters: true,
      renderHeaders: true,
      ignoreLastRenderedPageBreak: false
    });

    for (const [index, section] of Array.from(container.querySelectorAll<HTMLElement>('.docx')).entries()) {
      section.dataset.outlineKind = 'section';
      section.dataset.outlineLabel = `Secao ${index + 1}`;
      section.dataset.sectionTitle = `Secao ${index + 1}`;
    }

    return normalizeDomDocument({
      input,
      root: container,
      format: 'docx',
      outlineKind: 'section',
      limitations: ['A fidelidade de DOCX depende do HTML gerado pelo renderer no navegador. Recursos muito avancados de layout podem sofrer pequenas diferencas.']
    });
  });
}

async function parseXlsxDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const XLSX = await import('xlsx');
  const workbook = XLSX.read(await input.blob.arrayBuffer(), {
    type: 'array',
    cellStyles: true,
    cellDates: true,
    dense: true
  });

  return withOffscreenContainer((container) => {
    workbook.SheetNames.forEach((sheetName, index) => {
      const section = globalThis.document.createElement('section');
      section.dataset.outlineKind = 'sheet';
      section.dataset.outlineLabel = sheetName;
      section.dataset.sheetName = sheetName;
      section.dataset.sectionTitle = sheetName;
      const heading = globalThis.document.createElement('h2');
      heading.textContent = sheetName;
      const sheetHtml = XLSX.utils.sheet_to_html(workbook.Sheets[sheetName], {
        id: `sheet-${index + 1}`
      });
      const contentHost = globalThis.document.createElement('div');
      contentHost.innerHTML = sheetHtml;
      section.append(heading, ...Array.from(contentHost.childNodes));
      container.append(section);
    });

    return normalizeDomDocument({
      input,
      root: container,
      format: 'xlsx',
      outlineKind: 'sheet',
      limitations: ['Estilos de planilha que dependem de formulas visuais, graficos e merges complexos podem aparecer com degradacao parcial.']
    });
  });
}

async function parseImageDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const createId = createIdFactory(`${input.documentId}-image`);
  const imageUrl = URL.createObjectURL(input.blob);
  const pageId = createId('image-page');
  const imageId = createId('image');
  const root: DocumentNode[] = [{
    id: pageId,
    kind: 'page',
    label: 'Imagem original',
    sourceMap: { sourceType: 'image', pageNumber: 1, parserVersion: DOCUMENT_PARSER_VERSION },
    children: [{
      id: imageId,
      kind: 'image',
      src: imageUrl,
      alt: input.originalFileName,
      sourceMap: { sourceType: 'image', pageNumber: 1, parserVersion: DOCUMENT_PARSER_VERSION }
    }]
  }];
  const annotatedRoot = annotateTextRanges(root);
  const plainText = extractPlainText(annotatedRoot);

  return {
    model: {
      metadata: createDocumentMetadata({
        documentId: input.documentId,
        title: input.title,
        originalFileName: input.originalFileName,
        contentType: input.contentType,
        format: 'image',
        version: input.version,
        status: input.status,
        createdAtUtc: input.createdAtUtc,
        updatedAtUtc: input.updatedAtUtc,
        limitations: ['Imagens nao possuem estrutura textual nativa; a experiencia visual preserva a imagem original, mas nao gera texto sem OCR.'],
        supportsFidelity: true
      }),
      root: annotatedRoot,
      outline: [{ id: `${input.documentId}-image`, label: 'Imagem', kind: 'image', index: 1, targetNodeId: pageId }],
      assets: [{ assetId: imageId, kind: 'image', mimeType: input.contentType, src: imageUrl, name: input.originalFileName, sourceNodeId: imageId }],
      plainText,
      normalizedText: normalizeTextForMatching(plainText)
    },
    parserWarnings: ['A imagem foi preservada visualmente. Para obter texto indexavel, um pipeline de OCR precisa ser executado separadamente.']
  };
}

async function parseUnknownDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const fallback = await parseTextDocument({
    ...input,
    contentType: 'text/plain'
  });
  fallback.model.metadata.format = 'unknown';
  fallback.model.metadata.capabilities.supportsFidelity = false;
  fallback.model.metadata.capabilities.limitations.push('O formato nao possui parser especifico no viewer. Foi aplicado fallback textual sem reescrever o conteudo.');
  fallback.parserWarnings.push('Fallback textual aplicado por ausencia de parser especifico.');
  return fallback;
}

async function parsePdfDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const pdfjs = await import('pdfjs-dist/legacy/build/pdf.mjs');
  pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdfjs-dist/legacy/build/pdf.worker.min.mjs', import.meta.url).toString();

  const loadingTask = pdfjs.getDocument({
    data: new Uint8Array(await input.blob.arrayBuffer()),
    isEvalSupported: false,
    useWorkerFetch: false
  });
  const pdf = await loadingTask.promise;
  const createId = createIdFactory(`${input.documentId}-pdf`);
  const outline: DocumentOutlineItem[] = [];
  const root: DocumentNode[] = [];

  for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber += 1) {
    const page = await pdf.getPage(pageNumber);
    const viewport = page.getViewport({ scale: 1 });
    const textContent = await page.getTextContent();
    const lines = groupPdfLines(textContent.items as PdfInputItem[], viewport.height);
    const pageId = createId(`page-${pageNumber}`);
    root.push({
      id: pageId,
      kind: 'page',
      label: `Pagina ${pageNumber}`,
      layout: {
        position: 'block',
        width: viewport.width,
        height: viewport.height,
        unit: 'px'
      },
      style: {
        css: {
          width: `${viewport.width}px`,
          minHeight: `${viewport.height}px`,
          position: 'relative'
        }
      },
      sourceMap: {
        sourceType: 'page',
        pageNumber,
        parserVersion: DOCUMENT_PARSER_VERSION
      },
      children: lines.map((line, lineIndex) => ({
        id: createId(`page-${pageNumber}-line-${lineIndex + 1}`),
        kind: 'paragraph',
        layout: {
          position: 'absolute',
          x: line.x,
          y: line.y,
          width: Math.max(line.width, 1),
          height: Math.max(line.height, 1),
          unit: 'px'
        },
        style: {
          css: {
            position: 'absolute',
            left: `${line.x}px`,
            top: `${line.y}px`,
            width: `${Math.max(line.width, 1)}px`,
            minHeight: `${Math.max(line.height, 1)}px`,
            margin: 0,
            whiteSpace: 'pre'
          },
          preserveWhitespace: true
        },
        sourceMap: {
          sourceType: 'page',
          pageNumber,
          coordinates: [{ x: line.x, y: line.y, width: line.width, height: line.height, unit: 'px' }],
          parserVersion: DOCUMENT_PARSER_VERSION
        },
        children: line.items.map((item, itemIndex) => ({
          id: createId(`page-${pageNumber}-line-${lineIndex + 1}-text-${itemIndex + 1}`),
          kind: 'text',
          text: item.str,
          style: {
            css: {
              fontFamily: item.fontName || 'serif',
              fontSize: `${Math.max(item.fontSize, 8)}px`,
              fontWeight: /bold/i.test(item.fontName ?? '') ? '700' : undefined,
              fontStyle: /italic/i.test(item.fontName ?? '') ? 'italic' : undefined,
              whiteSpace: 'pre'
            },
            preserveWhitespace: true
          },
          sourceMap: {
            sourceType: 'page',
            pageNumber,
            coordinates: [{ x: item.x, y: item.y, width: item.width, height: item.height, unit: 'px' }],
            parserVersion: DOCUMENT_PARSER_VERSION
          }
        }))
      }))
    });
    outline.push({ id: `${pageId}-outline`, label: `Pagina ${pageNumber}`, kind: 'page', index: pageNumber, targetNodeId: pageId, pageNumber });
  }

  const annotatedRoot = annotateTextRanges(root);
  const plainText = extractPlainText(annotatedRoot);

  return {
    model: {
      metadata: createDocumentMetadata({
        documentId: input.documentId,
        title: input.title,
        originalFileName: input.originalFileName,
        contentType: input.contentType,
        format: 'pdf',
        version: input.version,
        status: input.status,
        createdAtUtc: input.createdAtUtc,
        updatedAtUtc: input.updatedAtUtc,
        limitations: ['PDF preserva texto, coordenadas e estilo basico. Imagens e vetores internos nao sao reconstituídos integralmente no viewer textual.']
      }),
      root: annotatedRoot,
      outline,
      assets: [],
      plainText,
      normalizedText: normalizeTextForMatching(plainText)
    },
    parserWarnings: []
  };
}

async function parsePptxDocument(input: DocumentParserInput): Promise<ParsedDocumentPayload> {
  const JSZip = (await import('jszip')).default;
  const zip = await JSZip.loadAsync(await input.blob.arrayBuffer());
  const createId = createIdFactory(`${input.documentId}-pptx`);
  const root: DocumentNode[] = [];
  const outline: DocumentOutlineItem[] = [];
  const slideFiles = zip.file(/^ppt\/slides\/slide\d+\.xml$/).sort((left, right) => comparePptSlidePath(left.name, right.name));
  const presentationXml = await zip.file('ppt/presentation.xml')?.async('string');
  const { width, height } = presentationXml ? parsePptSize(presentationXml) : { width: 1280, height: 720 };

  for (const [index, slideFile] of slideFiles.entries()) {
    const slideNumber = index + 1;
    const slideXml = await slideFile.async('string');
    const relsPath = `ppt/slides/_rels/${slideFile.name.split('/').pop()}.rels`;
    const relsXml = await zip.file(relsPath)?.async('string');
    const relationships = relsXml ? parseRelationships(relsXml) : new Map<string, string>();
    const slide = await parsePptSlide(slideXml, relationships, zip, {
      createId,
      slideNumber,
      width,
      height
    });
    root.push(slide);
    outline.push({ id: `${slide.id}-outline`, label: `Slide ${slideNumber}`, kind: 'slide', index: slideNumber, targetNodeId: slide.id, slideNumber });
  }

  const annotatedRoot = annotateTextRanges(root);
  const plainText = extractPlainText(annotatedRoot);

  return {
    model: {
      metadata: createDocumentMetadata({
        documentId: input.documentId,
        title: input.title,
        originalFileName: input.originalFileName,
        contentType: input.contentType,
        format: 'pptx',
        version: input.version,
        status: input.status,
        createdAtUtc: input.createdAtUtc,
        updatedAtUtc: input.updatedAtUtc,
        limitations: ['PPTX preserva blocos textuais, bullets, imagens e posicoes basicas. Animacoes, transicoes e recursos avancados de tema nao sao reproduzidos integralmente.']
      }),
      root: annotatedRoot,
      outline,
      assets: [],
      plainText,
      normalizedText: normalizeTextForMatching(plainText)
    },
    parserWarnings: []
  };
}

async function parsePptSlide(
  slideXml: string,
  relationships: Map<string, string>,
  zip: { file: (path: string) => { async: (type: 'base64') => Promise<string> } | null },
  options: {
    createId: ReturnType<typeof createIdFactory>;
    slideNumber: number;
    width: number;
    height: number;
  }
): Promise<DocumentNode> {
  const xml = new DOMParser().parseFromString(slideXml, 'application/xml');
  const shapeNodes = getByLocalName(xml.documentElement, 'sp')
    .map((shape) => buildPptShapeNode(shape, options.createId, options.slideNumber))
    .filter((node): node is DocumentNode => node !== null);
  const imageNodes = await buildPptImageNodes(getByLocalName(xml.documentElement, 'pic'), relationships, zip, options);
  const slideChildren: DocumentNode[] = [...shapeNodes, ...imageNodes];

  return {
    id: options.createId(`slide-${options.slideNumber}`),
    kind: 'slide',
    label: `Slide ${options.slideNumber}`,
    layout: { position: 'block', width: options.width, height: options.height, unit: 'px' },
    style: {
      css: {
        width: `${options.width}px`,
        minHeight: `${options.height}px`,
        position: 'relative'
      }
    },
    sourceMap: {
      sourceType: 'slide',
      slideNumber: options.slideNumber,
      parserVersion: DOCUMENT_PARSER_VERSION
    },
    children: slideChildren
  };
}

function buildPptShapeNode(shape: Element, createId: ReturnType<typeof createIdFactory>, slideNumber: number): DocumentNode | null {
  const textParagraphs = getByLocalName(shape, 'p').map((paragraphNode) => paragraphFromPptParagraph(paragraphNode, createId, slideNumber));
  if (textParagraphs.length === 0) {
    return null;
  }

  const position = readPptTransform(shape);
  const isTitle = getByLocalName(shape, 'ph').some((placeholder) => (placeholder.getAttribute('type') ?? '').includes('title'));
  const sourceMap = buildPptSourceMap(slideNumber, position);

  return {
    id: createId(`slide-${slideNumber}-shape`),
    kind: isTitle ? 'heading' : 'container',
    level: isTitle ? 2 : undefined,
    layout: position ? { position: 'absolute', ...position, unit: 'px' } : undefined,
    style: {
      css: toAbsoluteCss(position)
    },
    sourceMap,
    children: isTitle ? flattenParagraphChildren(textParagraphs) : normalizePptParagraphGroup(textParagraphs, createId, slideNumber)
  } as DocumentNode;
}

async function buildPptImageNodes(
  pictures: Element[],
  relationships: Map<string, string>,
  zip: { file: (path: string) => { async: (type: 'base64') => Promise<string> } | null },
  options: {
    createId: ReturnType<typeof createIdFactory>;
    slideNumber: number;
    width: number;
    height: number;
  }
) {
  const imageNodes: DocumentNode[] = [];
  for (const picture of pictures) {
    const imageNode = await buildPptImageNode(picture, relationships, zip, options.createId, options.slideNumber);
    if (imageNode) {
      imageNodes.push(imageNode);
    }
  }

  return imageNodes;
}

async function buildPptImageNode(
  picture: Element,
  relationships: Map<string, string>,
  zip: { file: (path: string) => { async: (type: 'base64') => Promise<string> } | null },
  createId: ReturnType<typeof createIdFactory>,
  slideNumber: number
): Promise<DocumentNode | null> {
  const relationshipId = getByLocalName(picture, 'blip')[0]?.getAttribute('r:embed') ?? '';
  const imagePath = relationships.get(relationshipId);
  if (!imagePath) {
    return null;
  }

  const zipPath = `ppt/${imagePath.replace(/^\.\//, '')}`;
  const file = zip.file(zipPath);
  if (!file) {
    return null;
  }

  const base64 = await file.async('base64');
  const position = readPptTransform(picture);
  return {
    id: createId(`slide-${slideNumber}-image`),
    kind: 'image' as const,
    src: `data:${guessMimeTypeFromPath(zipPath)};base64,${base64}`,
    alt: getByLocalName(picture, 'cNvPr')[0]?.getAttribute('name') ?? `Imagem do slide ${slideNumber}`,
    width: position?.width,
    height: position?.height,
    layout: position ? { position: 'absolute', ...position, unit: 'px' } : undefined,
    style: {
      css: toAbsoluteCss(position, true)
    },
    sourceMap: buildPptSourceMap(slideNumber, position)
  };
}

function paragraphFromPptParagraph(paragraphNode: Element, createId: ReturnType<typeof createIdFactory>, slideNumber: number): DocumentNode {
  const runs = getByLocalName(paragraphNode, 'r');
  const textChildren = runs.length > 0
    ? runs.map((runNode) => {
      const textNode = getByLocalName(runNode, 't')[0];
      const runProps = getByLocalName(runNode, 'rPr')[0];
      const fontSize = runProps?.getAttribute('sz');
      return {
        id: createId('ppt-run'),
        kind: 'text',
        text: textNode?.textContent ?? '',
        style: {
          css: {
            fontWeight: runProps?.getAttribute('b') === '1' ? '700' : undefined,
            fontStyle: runProps?.getAttribute('i') === '1' ? 'italic' : undefined,
            textDecorationLine: runProps?.getAttribute('u') ? 'underline' : undefined,
            fontSize: fontSize ? `${Number(fontSize) / 100}pt` : undefined
          }
        },
        sourceMap: {
          sourceType: 'slide',
          slideNumber,
          parserVersion: DOCUMENT_PARSER_VERSION
        }
      } as DocumentNode;
    })
    : [{
      id: createId('ppt-text'),
      kind: 'text',
      text: getByLocalName(paragraphNode, 't').map((node) => node.textContent ?? '').join(' '),
      sourceMap: {
        sourceType: 'slide',
        slideNumber,
        parserVersion: DOCUMENT_PARSER_VERSION
      }
    } as DocumentNode];

  const hasBullet = getByLocalName(paragraphNode, 'buChar').length > 0;
  return {
    id: createId('ppt-paragraph'),
    kind: hasBullet ? 'list-item' : 'paragraph',
    sourceMap: {
      sourceType: 'slide',
      slideNumber,
      parserVersion: DOCUMENT_PARSER_VERSION
    },
    children: textChildren
  };
}

function normalizePptParagraphGroup(paragraphs: DocumentNode[], createId: ReturnType<typeof createIdFactory>, slideNumber: number): DocumentNode[] {
  const bulletParagraphs = paragraphs.filter((paragraph) => paragraph.kind === 'list-item');
  if (bulletParagraphs.length === paragraphs.length && bulletParagraphs.length > 0) {
    return [{
      id: createId('ppt-list'),
      kind: 'list',
      ordered: false,
      sourceMap: {
        sourceType: 'slide',
        slideNumber,
        parserVersion: DOCUMENT_PARSER_VERSION
      },
      children: bulletParagraphs
    }];
  }

  return paragraphs.map((paragraph) => paragraph.kind === 'list-item'
    ? {
      id: createId('ppt-list-wrapper'),
      kind: 'list',
      ordered: false,
      sourceMap: {
        sourceType: 'slide',
        slideNumber,
        parserVersion: DOCUMENT_PARSER_VERSION
      },
      children: [paragraph]
    }
    : paragraph);
}

function flattenParagraphChildren(paragraphs: DocumentNode[]) {
  return paragraphs.flatMap((paragraph) => ('children' in paragraph ? paragraph.children : [paragraph]));
}

function groupPdfLines(items: PdfInputItem[], pageHeight: number) {
  const buckets = new Map<number, PdfTextItem[]>();

  for (const item of items.filter(isPdfTextItem)) {
    const y = Math.round((pageHeight - item.transform[5]) / 4) * 4;
    const bucket = buckets.get(y) ?? [];
    bucket.push({
      ...item,
      x: item.transform[4],
      y,
      width: item.width,
      height: item.height,
      fontSize: item.height || Math.abs(item.transform[0]),
      fontName: item.fontName ?? ''
    });
    buckets.set(y, bucket);
  }

  return Array.from(buckets.entries())
    .sort(([leftY], [rightY]) => leftY - rightY)
    .map(([y, bucket]) => {
      const sortedItems = [...bucket].sort((left, right) => left.x - right.x);
      const width = sortedItems.reduce((maxWidth, item) => Math.max(maxWidth, item.x + item.width), 0) - sortedItems[0].x;
      const height = sortedItems.reduce((maxHeight, item) => Math.max(maxHeight, item.height), 0);
      return {
        y,
        x: sortedItems[0].x,
        width,
        height,
        items: sortedItems
      };
    });
}

async function withOffscreenContainer<T>(callback: (container: HTMLDivElement) => T | Promise<T>) {
  const container = globalThis.document.createElement('div');
  container.style.position = 'fixed';
  container.style.left = '-200vw';
  container.style.top = '0';
  container.style.width = '1400px';
  container.style.pointerEvents = 'none';
  container.style.opacity = '0';
  globalThis.document.body.append(container);

  try {
    return await callback(container);
  } finally {
    container.remove();
  }
}

function parseRelationships(xmlContent: string) {
  const xml = new DOMParser().parseFromString(xmlContent, 'application/xml');
  const relationships = new Map<string, string>();
  for (const relationship of Array.from(xml.getElementsByTagName('Relationship'))) {
    const id = relationship.getAttribute('Id');
    const target = relationship.getAttribute('Target');
    if (id && target) {
      relationships.set(id, target);
    }
  }

  return relationships;
}

function parsePptSize(xmlContent: string) {
  const xml = new DOMParser().parseFromString(xmlContent, 'application/xml');
  const slideSize = getByLocalName(xml.documentElement, 'sldSz')[0];
  const cx = Number(slideSize?.getAttribute('cx') ?? 12192000);
  const cy = Number(slideSize?.getAttribute('cy') ?? 6858000);
  return {
    width: Math.round(cx / EMU_TO_PX),
    height: Math.round(cy / EMU_TO_PX)
  };
}

function readPptTransform(node: Element) {
  const transform = getByLocalName(node, 'xfrm')[0];
  const offset = getByLocalName(transform, 'off')[0];
  const extent = getByLocalName(transform, 'ext')[0];
  if (!offset || !extent) {
    return undefined;
  }

  return {
    x: Math.round(Number(offset.getAttribute('x') ?? 0) / EMU_TO_PX),
    y: Math.round(Number(offset.getAttribute('y') ?? 0) / EMU_TO_PX),
    width: Math.round(Number(extent.getAttribute('cx') ?? 0) / EMU_TO_PX),
    height: Math.round(Number(extent.getAttribute('cy') ?? 0) / EMU_TO_PX)
  };
}

function getByLocalName(root: Element | Document, localName: string) {
  return Array.from(root.getElementsByTagName('*')).filter((node) => node.localName === localName);
}

function comparePptSlidePath(left: string, right: string) {
  return extractTrailingNumber(left) - extractTrailingNumber(right);
}

function extractTrailingNumber(value: string) {
  const match = /(\d+)/.exec(value);
  return match ? Number(match[1]) : 0;
}

function buildPptSourceMap(slideNumber: number, position?: { x: number; y: number; width: number; height: number }) {
  return {
    sourceType: 'slide' as const,
    slideNumber,
    coordinates: position ? [{ x: position.x, y: position.y, width: position.width, height: position.height, unit: 'px' as const }] : undefined,
    parserVersion: DOCUMENT_PARSER_VERSION
  };
}

function toAbsoluteCss(position?: { x: number; y: number; width: number; height: number }, fixedHeight = false) {
  if (!position) {
    return undefined;
  }

  return {
    position: 'absolute' as const,
    left: `${position.x}px`,
    top: `${position.y}px`,
    width: `${position.width}px`,
    ...(fixedHeight ? { height: `${position.height}px` } : { minHeight: `${position.height}px` })
  };
}

function guessMimeTypeFromPath(path: string) {
  const normalized = path.toLowerCase();
  if (normalized.endsWith('.png')) {
    return 'image/png';
  }

  if (normalized.endsWith('.jpg') || normalized.endsWith('.jpeg')) {
    return 'image/jpeg';
  }

  if (normalized.endsWith('.gif')) {
    return 'image/gif';
  }

  if (normalized.endsWith('.svg')) {
    return 'image/svg+xml';
  }

  return 'application/octet-stream';
}

type PdfTextItem = {
  str: string;
  transform: number[];
  width: number;
  height: number;
  fontName?: string;
  x: number;
  y: number;
  fontSize: number;
};

type PdfInputItem = {
  str?: string;
  transform?: number[];
  width?: number;
  height?: number;
  fontName?: string;
};

function isPdfTextItem(item: PdfInputItem): item is PdfTextItem {
  return typeof item.str === 'string'
    && item.str.trim().length > 0
    && Array.isArray(item.transform)
    && typeof item.width === 'number'
    && typeof item.height === 'number';
}