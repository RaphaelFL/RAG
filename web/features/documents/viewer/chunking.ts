import type { DocumentChunkInspection } from '@/features/documents/types/documents';
import type { ChunkGenerationOptions, ChunkSourceMap, ChunkingStrategyId, DocumentNode, UnifiedDocumentModel, ViewerChunk } from '@/features/documents/viewer/types';
import { buildNormalizedIndex, buildOriginLabel, collectNodeIds, computeStableHash, createFallbackChunk, extractNodeText, normalizeTextForMatching, renderNodesToHtml } from '@/features/documents/viewer/utils';

type ChunkableUnit = {
  node: DocumentNode;
  text: string;
  headingAncestry: string[];
  sourceMap: ChunkSourceMap;
  metadata: Record<string, string>;
};

export function buildViewerChunksFromInspection(
  model: UnifiedDocumentModel,
  documentId: string,
  originalFileName: string,
  chunks: DocumentChunkInspection[]
) {
  const index = buildNormalizedIndex(model.plainText);

  return chunks.map((chunk) => {
    const sourceType = resolveChunkSourceType(chunk.section, chunk.pageNumber);
    const fallbackSourceMap = {
      sourceType,
      pageNumber: chunk.pageNumber || undefined,
      endPageNumber: chunk.endPageNumber ?? undefined,
      sectionTitle: chunk.section ?? undefined
    } satisfies ChunkSourceMap;
    const hasUnavailableContent = isUnavailableChunkContent(chunk.content, originalFileName);
    const matchedRange = hasUnavailableContent ? undefined : matchDocumentRange(index, chunk.content);

    if (!matchedRange) {
      const sourceProjection = buildChunkFromSourceProjection(model.root, {
        chunkId: chunk.chunkId,
        chunkIndex: chunk.chunkIndex,
        documentId,
        referenceFileName: originalFileName,
        metadata: {
          ...chunk.metadata,
          embeddingDimensions: String(chunk.embedding.dimensions),
          pageNumber: String(chunk.pageNumber),
          section: chunk.section ?? ''
        },
        sourceMap: fallbackSourceMap
      });

      if (sourceProjection) {
        return sourceProjection;
      }

      return createFallbackChunk({
        chunkId: chunk.chunkId,
        documentId,
        chunkIndex: chunk.chunkIndex,
        rawText: chunk.content,
        referenceFileName: originalFileName,
        metadata: {
          ...chunk.metadata,
          pageNumber: String(chunk.pageNumber),
          section: chunk.section ?? ''
        },
        sourceMap: fallbackSourceMap
      });
    }

    const formattedContent = projectNodesByRange(model.root, matchedRange.startOffset, matchedRange.endOffset, true);
    const mergedSourceMap = mergeSourceMaps(formattedContent, fallbackSourceMap);
    const headingAncestry = deriveHeadingAncestry(model.root, matchedRange.startOffset, matchedRange.endOffset);
    const rawText = chunk.content;

    return {
      chunkId: chunk.chunkId,
      documentId,
      chunkIndex: chunk.chunkIndex,
      backendChunkId: chunk.chunkId,
      strategyId: 'backend-synced',
      sourceType: mergedSourceMap.sourceType,
      rawText,
      normalizedText: normalizeTextForMatching(rawText),
      formattedContent: formattedContent.length > 0 ? formattedContent : createFallbackChunk({
        chunkId: chunk.chunkId,
        documentId,
        chunkIndex: chunk.chunkIndex,
        rawText,
        referenceFileName: originalFileName,
        metadata: chunk.metadata,
        sourceMap: mergedSourceMap
      }).formattedContent,
      formattedHtml: renderNodesToHtml(formattedContent),
      contentHash: computeStableHash(rawText),
      referenceFileName: originalFileName,
      parserVersion: model.metadata.parserVersion,
      originLabel: buildOriginLabel(mergedSourceMap),
      headingAncestry,
      sourceMap: mergedSourceMap,
      metadata: {
        ...chunk.metadata,
        embeddingDimensions: String(chunk.embedding.dimensions),
        pageNumber: String(chunk.pageNumber),
        section: chunk.section ?? ''
      },
      fragments: [{
        fragmentId: `${chunk.chunkId}-fragment`,
        nodeIds: collectNodeIds(formattedContent),
        sourceMap: mergedSourceMap
      }],
      sourceNodeIds: collectNodeIds(formattedContent),
      confidence: matchedRange.confidence
    } satisfies ViewerChunk;
  });
}

export function buildLocalStrategyChunks(
  model: UnifiedDocumentModel,
  documentId: string,
  originalFileName: string,
  strategyId: Exclude<ChunkingStrategyId, 'backend-synced'>,
  options: ChunkGenerationOptions
) {
  const units = collectChunkableUnits(model.root);
  if (units.length === 0) {
    return [];
  }

  const groups = groupUnitsByStrategy(units, strategyId, options);
  return groups.map((group, index) => createChunkFromUnits(group, {
    chunkId: `${documentId}-${strategyId}-${String(index + 1).padStart(4, '0')}`,
    chunkIndex: index + 1,
    documentId,
    originalFileName,
    parserVersion: model.metadata.parserVersion,
    strategyId
  }));
}

function createChunkFromUnits(
  units: ChunkableUnit[],
  options: {
    chunkId: string;
    chunkIndex: number;
    documentId: string;
    originalFileName: string;
    parserVersion: string;
    strategyId: Exclude<ChunkingStrategyId, 'backend-synced'>;
  }
): ViewerChunk {
  const startOffset = Math.min(...units.map((unit) => unit.sourceMap.startOffset ?? Number.MAX_SAFE_INTEGER));
  const endOffset = Math.max(...units.map((unit) => unit.sourceMap.endOffset ?? 0));
  const sourceMap = mergeSourceMaps(units.map((unit) => unit.node), {
    sourceType: units[0]?.sourceMap.sourceType ?? 'range',
    startOffset,
    endOffset
  });
  const formattedContent = projectNodesByRange(units.map((unit) => unit.node), startOffset, endOffset, true);
  const rawText = formattedContent.map((node) => extractNodeText(node)).join('').trim();

  return {
    chunkId: options.chunkId,
    documentId: options.documentId,
    chunkIndex: options.chunkIndex,
    strategyId: options.strategyId,
    sourceType: sourceMap.sourceType,
    rawText,
    normalizedText: normalizeTextForMatching(rawText),
    formattedContent,
    formattedHtml: renderNodesToHtml(formattedContent),
    contentHash: computeStableHash(rawText),
    referenceFileName: options.originalFileName,
    parserVersion: options.parserVersion,
    originLabel: buildOriginLabel(sourceMap),
    headingAncestry: units[0]?.headingAncestry ?? [],
    sourceMap,
    metadata: {
      strategy: options.strategyId,
      units: String(units.length)
    },
    fragments: [{
      fragmentId: `${options.chunkId}-fragment`,
      nodeIds: units.flatMap((unit) => collectNodeIds([unit.node])),
      sourceMap
    }],
    sourceNodeIds: units.flatMap((unit) => collectNodeIds([unit.node]))
  };
}

function collectChunkableUnits(nodes: DocumentNode[], headingAncestry: string[] = []): ChunkableUnit[] {
  return nodes.flatMap((node) => {
    if (node.kind === 'heading') {
      const nextHeadingAncestry = [...headingAncestry.slice(0, Math.max((node.level ?? 2) - 1, 0)), extractNodeText(node).trim()].filter(Boolean);
      return [{
        node,
        text: extractNodeText(node).trim(),
        headingAncestry: nextHeadingAncestry,
        sourceMap: node.sourceMap ?? { sourceType: 'heading' },
        metadata: { kind: node.kind }
      }];
    }

    if (['paragraph', 'list', 'table', 'image', 'list-item'].includes(node.kind)) {
      return [{
        node,
        text: extractNodeText(node).trim(),
        headingAncestry,
        sourceMap: node.sourceMap ?? { sourceType: 'range' },
        metadata: { kind: node.kind }
      }];
    }

    if ('children' in node) {
      const nextHeadingAncestry = node.kind === 'section' && node.label
        ? [...headingAncestry, node.label]
        : headingAncestry;
      return collectChunkableUnits(node.children, nextHeadingAncestry);
    }

    return [];
  }).filter((unit) => unit.text || unit.node.kind === 'image');
}

function groupUnitsByStrategy(units: ChunkableUnit[], strategyId: Exclude<ChunkingStrategyId, 'backend-synced'>, options: ChunkGenerationOptions) {
  switch (strategyId) {
    case 'paragraph':
    case 'semantic-block':
      return units.map((unit) => [unit]);
    case 'page':
      return groupByKey(units, (unit) => unit.sourceMap.pageNumber ? `page:${unit.sourceMap.pageNumber}` : fallbackStructuralKey(unit));
    case 'section':
      return groupByKey(units, (unit) => fallbackStructuralKey(unit));
    case 'heading':
      return groupByKey(units, (unit) => unit.headingAncestry.at(-1) || fallbackStructuralKey(unit));
    case 'character-limit':
      return buildCharacterLimitGroups(units, options.maxCharacters, options.overlapBlocks);
    default:
      return units.map((unit) => [unit]);
  }
}

function buildCharacterLimitGroups(units: ChunkableUnit[], maxCharacters: number, overlapBlocks: number) {
  const groups: ChunkableUnit[][] = [];
  let current: ChunkableUnit[] = [];
  let currentSize = 0;

  for (const unit of units) {
    const unitLength = unit.text.length;
    if (current.length > 0 && currentSize + unitLength > maxCharacters) {
      groups.push(current);
      current = overlapBlocks > 0 ? current.slice(Math.max(current.length - overlapBlocks, 0)) : [];
      currentSize = current.reduce((sum, item) => sum + item.text.length, 0);
    }

    current.push(unit);
    currentSize += unitLength;
  }

  if (current.length > 0) {
    groups.push(current);
  }

  return groups;
}

function groupByKey(units: ChunkableUnit[], keySelector: (unit: ChunkableUnit) => string) {
  const buckets = new Map<string, ChunkableUnit[]>();
  for (const unit of units) {
    const key = keySelector(unit);
    const bucket = buckets.get(key) ?? [];
    bucket.push(unit);
    buckets.set(key, bucket);
  }

  return Array.from(buckets.values());
}

function fallbackStructuralKey(unit: ChunkableUnit) {
  if (unit.sourceMap.sheetName) {
    return `sheet:${unit.sourceMap.sheetName}`;
  }

  if (unit.sourceMap.slideNumber) {
    return `slide:${unit.sourceMap.slideNumber}`;
  }

  if (unit.sourceMap.sectionTitle) {
    return `section:${unit.sourceMap.sectionTitle}`;
  }

  if (unit.sourceMap.pageNumber) {
    return `page:${unit.sourceMap.pageNumber}`;
  }

  return unit.headingAncestry.at(-1) || 'document';
}

function matchDocumentRange(index: ReturnType<typeof buildNormalizedIndex>, content: string) {
  const target = buildNormalizedIndex(content).normalized;
  if (!target) {
    return undefined;
  }

  const matchIndex = index.normalized.indexOf(target);
  if (matchIndex < 0) {
    return undefined;
  }

  const startOffset = index.originalOffsets[matchIndex] ?? 0;
  const endNormalizedIndex = Math.min(matchIndex + target.length - 1, index.originalOffsets.length - 1);
  const endOffset = (index.originalOffsets[endNormalizedIndex] ?? startOffset) + 1;

  return {
    startOffset,
    endOffset,
    confidence: Math.min(1, target.length / Math.max(content.length, 1))
  };
}

export function projectNodesByRange(nodes: DocumentNode[], startOffset: number, endOffset: number, includeWholeStructure = false): DocumentNode[] {
  return nodes.flatMap((node) => projectNodeByRange(node, startOffset, endOffset, includeWholeStructure)).filter(Boolean) as DocumentNode[];
}

function projectNodeByRange(node: DocumentNode, startOffset: number, endOffset: number, includeWholeStructure: boolean): DocumentNode | null {
  const nodeStart = node.sourceMap?.startOffset;
  const nodeEnd = node.sourceMap?.endOffset;
  const intersects = nodeStart === undefined || nodeEnd === undefined
    ? includeWholeStructure
    : nodeEnd > startOffset && nodeStart < endOffset;

  if (!intersects) {
    return null;
  }

  if (node.kind === 'text') {
    if (nodeStart === undefined || nodeEnd === undefined) {
      return node;
    }

    const sliceStart = Math.max(startOffset - nodeStart, 0);
    const sliceEnd = Math.min(endOffset - nodeStart, node.text.length);
    return {
      ...node,
      text: node.text.slice(sliceStart, sliceEnd)
    };
  }

  if (node.kind === 'line-break' || node.kind === 'image') {
    return node;
  }

  if (includeWholeStructure && preserveWholeNode(node.kind)) {
    return cloneNode(node);
  }

  const children = node.children
    .flatMap((child) => projectNodeByRange(child, startOffset, endOffset, includeWholeStructure))
    .filter(Boolean) as DocumentNode[];

  if (children.length === 0) {
    return null;
  }

  const imageChildren = node.children.filter((child) => child.kind === 'image');
  const mergedChildren = imageChildren.length > 0 && !children.some((child) => child.kind === 'image')
    ? [...children, ...imageChildren.map(cloneNode)]
    : children;

  return {
    ...node,
    children: mergedChildren
  };
}

function preserveWholeNode(kind: DocumentNode['kind']) {
  return kind === 'heading' || kind === 'list' || kind === 'list-item' || kind === 'table' || kind === 'table-row' || kind === 'table-cell';
}

function cloneNode(node: DocumentNode): DocumentNode {
  if (node.kind === 'text' || node.kind === 'line-break' || node.kind === 'image') {
    return { ...node };
  }

  return {
    ...node,
    children: node.children.map((child) => cloneNode(child))
  };
}

function mergeSourceMaps(nodes: DocumentNode[], fallback: ChunkSourceMap): ChunkSourceMap {
  const sourceMaps = collectSourceMaps(nodes);
  const first = sourceMaps[0] ?? fallback;
  const last = sourceMaps.at(-1) ?? fallback;

  return {
    ...fallback,
    ...first,
    endPageNumber: last.endPageNumber ?? last.pageNumber ?? first.endPageNumber,
    startOffset: Math.min(...sourceMaps.map((item) => item.startOffset ?? Number.MAX_SAFE_INTEGER), fallback.startOffset ?? Number.MAX_SAFE_INTEGER),
    endOffset: Math.max(...sourceMaps.map((item) => item.endOffset ?? 0), fallback.endOffset ?? 0),
    coordinates: sourceMaps.flatMap((item) => item.coordinates ?? [])
  };
}

function collectSourceMaps(nodes: DocumentNode[]): ChunkSourceMap[] {
  const result: ChunkSourceMap[] = [];
  for (const node of nodes) {
    if (node.sourceMap) {
      result.push(node.sourceMap);
    }
    if ('children' in node) {
      result.push(...collectSourceMaps(node.children));
    }
  }

  return result;
}

function deriveHeadingAncestry(nodes: DocumentNode[], startOffset: number, endOffset: number, current: string[] = []): string[] {
  for (const node of nodes) {
    const next = node.kind === 'heading'
      ? [...current.slice(0, Math.max((node.level ?? 2) - 1, 0)), extractNodeText(node).trim()].filter(Boolean)
      : current;
    const nodeStart = node.sourceMap?.startOffset;
    const nodeEnd = node.sourceMap?.endOffset;
    const intersects = nodeStart === undefined || nodeEnd === undefined || (nodeEnd > startOffset && nodeStart < endOffset);
    if (!intersects) {
      continue;
    }

    if ('children' in node) {
      const nested = deriveHeadingAncestry(node.children, startOffset, endOffset, next);
      if (nested.length > 0) {
        return nested;
      }
    }

    if (node.kind === 'heading') {
      return next;
    }
  }

  return current;
}

function resolveChunkSourceType(section: string | null | undefined, pageNumber: number) {
  if (section) {
    return 'section' as const;
  }

  if (pageNumber) {
    return 'page' as const;
  }

  return 'range' as const;
}

function buildChunkFromSourceProjection(
  nodes: DocumentNode[],
  chunk: {
    chunkId: string;
    chunkIndex: number;
    documentId: string;
    referenceFileName: string;
    metadata: Record<string, string>;
    sourceMap: ChunkSourceMap;
  }
): ViewerChunk | null {
  const formattedContent = projectNodesBySource(nodes, chunk.sourceMap);
  if (formattedContent.length === 0) {
    return null;
  }

  const rawText = formattedContent.map((node) => extractNodeText(node)).join('').trim();
  if (!rawText) {
    return null;
  }

  const mergedSourceMap = mergeSourceMaps(formattedContent, chunk.sourceMap);
  const headingAncestry = mergedSourceMap.startOffset !== undefined && mergedSourceMap.endOffset !== undefined
    ? deriveHeadingAncestry(nodes, mergedSourceMap.startOffset, mergedSourceMap.endOffset)
    : [];

  return {
    chunkId: chunk.chunkId,
    documentId: chunk.documentId,
    chunkIndex: chunk.chunkIndex,
    backendChunkId: chunk.chunkId,
    strategyId: 'backend-synced',
    sourceType: mergedSourceMap.sourceType,
    rawText,
    normalizedText: normalizeTextForMatching(rawText),
    formattedContent,
    formattedHtml: renderNodesToHtml(formattedContent),
    contentHash: computeStableHash(rawText),
    referenceFileName: chunk.referenceFileName,
    parserVersion: mergedSourceMap.parserVersion ?? 'source-projection',
    originLabel: buildOriginLabel(mergedSourceMap),
    headingAncestry,
    sourceMap: mergedSourceMap,
    metadata: chunk.metadata,
    fragments: [{
      fragmentId: `${chunk.chunkId}-fragment`,
      nodeIds: collectNodeIds(formattedContent),
      sourceMap: mergedSourceMap
    }],
    sourceNodeIds: collectNodeIds(formattedContent),
    confidence: 0.2
  } satisfies ViewerChunk;
}

function projectNodesBySource(nodes: DocumentNode[], sourceMap: ChunkSourceMap) {
  return nodes.flatMap((node) => projectNodeBySource(node, sourceMap)).filter(Boolean) as DocumentNode[];
}

function projectNodeBySource(node: DocumentNode, sourceMap: ChunkSourceMap): DocumentNode | null {
  const nodeSourceMap = node.sourceMap;
  if (nodeSourceMap && sourceMapMatches(nodeSourceMap, sourceMap)) {
    return cloneNode(node);
  }

  if (!('children' in node)) {
    return null;
  }

  const children = node.children
    .flatMap((child) => projectNodeBySource(child, sourceMap))
    .filter(Boolean) as DocumentNode[];

  if (children.length === 0) {
    return null;
  }

  return {
    ...node,
    children
  };
}

function sourceMapMatches(candidate: ChunkSourceMap, target: ChunkSourceMap) {
  if (target.sectionTitle && candidate.sectionTitle) {
    return candidate.sectionTitle.localeCompare(target.sectionTitle, undefined, { sensitivity: 'accent' }) === 0;
  }

  if (target.pageNumber) {
    const candidateStart = candidate.pageNumber ?? candidate.endPageNumber ?? 0;
    const candidateEnd = candidate.endPageNumber ?? candidate.pageNumber ?? candidateStart;
    const targetEnd = target.endPageNumber ?? target.pageNumber;
    return candidateStart <= targetEnd && candidateEnd >= target.pageNumber;
  }

  return false;
}

function isUnavailableChunkContent(content: string, originalFileName: string) {
  const normalizedContent = normalizeTextForMatching(content);
  const normalizedFileName = normalizeTextForMatching(originalFileName);
  return normalizedContent === `conteudo indisponivel para ${normalizedFileName}`;
}