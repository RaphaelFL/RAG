import type { CSSProperties } from 'react';

export type SupportedDocumentFormat = 'pdf' | 'docx' | 'xlsx' | 'pptx' | 'txt' | 'html' | 'image' | 'unknown';
export type ChunkDisplayMode = 'clean' | 'fidelity';
export type ChunkingStrategyId = 'backend-synced' | 'paragraph' | 'semantic-block' | 'page' | 'section' | 'heading' | 'character-limit';
export type OutlineKind = 'document' | 'page' | 'section' | 'sheet' | 'slide' | 'image';
export type ChunkSourceType = 'document' | 'page' | 'section' | 'sheet' | 'slide' | 'table' | 'heading' | 'paragraph' | 'image' | 'range';

export type ChunkStyle = {
  css?: Partial<CSSProperties>;
  preserveWhitespace?: boolean;
};

export type SourceCoordinate = {
  x: number;
  y: number;
  width?: number;
  height?: number;
  unit: 'px' | 'percent';
};

export type ChunkSourceMap = {
  sourceType: ChunkSourceType;
  pageNumber?: number;
  endPageNumber?: number | null;
  sectionId?: string;
  sectionTitle?: string;
  sheetName?: string;
  slideNumber?: number;
  headingPath?: string[];
  startOffset?: number;
  endOffset?: number;
  coordinates?: SourceCoordinate[];
  parserVersion?: string;
};

export type NodeLayout = {
  position?: 'block' | 'absolute';
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  unit?: 'px' | 'percent';
  zIndex?: number;
};

type BaseNode = {
  id: string;
  kind:
    | 'document'
    | 'container'
    | 'page'
    | 'section'
    | 'sheet'
    | 'slide'
    | 'paragraph'
    | 'heading'
    | 'list'
    | 'list-item'
    | 'table'
    | 'table-row'
    | 'table-cell'
    | 'link'
    | 'image'
    | 'text'
    | 'line-break';
  style?: ChunkStyle;
  sourceMap?: ChunkSourceMap;
  layout?: NodeLayout;
  metadata?: Record<string, string>;
};

export type StructuredNode = BaseNode & {
  kind:
    | 'document'
    | 'container'
    | 'page'
    | 'section'
    | 'sheet'
    | 'slide'
    | 'paragraph'
    | 'heading'
    | 'list'
    | 'list-item'
    | 'table'
    | 'table-row'
    | 'table-cell'
    | 'link';
  children: DocumentNode[];
  label?: string;
  level?: number;
  ordered?: boolean;
  outlineId?: string;
};

export type TextNode = BaseNode & {
  kind: 'text';
  text: string;
};

export type LineBreakNode = BaseNode & {
  kind: 'line-break';
};

export type ImageNode = BaseNode & {
  kind: 'image';
  src: string;
  alt?: string;
  width?: number;
  height?: number;
  caption?: string;
};

export type DocumentNode = StructuredNode | TextNode | LineBreakNode | ImageNode;

export type DocumentOutlineItem = {
  id: string;
  label: string;
  kind: OutlineKind;
  index: number;
  targetNodeId: string;
  pageNumber?: number;
  sectionTitle?: string;
  sheetName?: string;
  slideNumber?: number;
};

export type DocumentAsset = {
  assetId: string;
  kind: 'image';
  mimeType: string;
  src: string;
  name?: string;
  sourceNodeId?: string;
};

export type DocumentMetadataModel = {
  documentId: string;
  title: string;
  originalFileName: string;
  contentType: string;
  format: SupportedDocumentFormat;
  parserVersion: string;
  status?: string;
  version?: number;
  createdAtUtc?: string;
  updatedAtUtc?: string | null;
  contentHash?: string;
  warnings: string[];
  capabilities: {
    supportsFidelity: boolean;
    supportsOriginalView: boolean;
    limitations: string[];
  };
};

export type UnifiedDocumentModel = {
  metadata: DocumentMetadataModel;
  root: DocumentNode[];
  outline: DocumentOutlineItem[];
  assets: DocumentAsset[];
  plainText: string;
  normalizedText: string;
};

export type ChunkFragment = {
  fragmentId: string;
  nodeIds: string[];
  sourceMap: ChunkSourceMap;
};

export type ViewerChunk = {
  chunkId: string;
  documentId: string;
  chunkIndex: number;
  backendChunkId?: string;
  strategyId: ChunkingStrategyId;
  sourceType: ChunkSourceType;
  rawText: string;
  normalizedText: string;
  formattedContent: DocumentNode[];
  formattedHtml?: string;
  contentHash: string;
  referenceFileName: string;
  parserVersion: string;
  originLabel: string;
  headingAncestry: string[];
  sourceMap: ChunkSourceMap;
  metadata: Record<string, string>;
  fragments: ChunkFragment[];
  sourceNodeIds: string[];
  confidence?: number;
};

export type ParsedDocumentPayload = {
  model: UnifiedDocumentModel;
  parserWarnings: string[];
};

export type DocumentParserInput = {
  documentId: string;
  title: string;
  originalFileName: string;
  contentType: string;
  version?: number;
  status?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string | null;
  blob: Blob;
};

export type ChunkGenerationOptions = {
  maxCharacters: number;
  overlapBlocks: number;
};