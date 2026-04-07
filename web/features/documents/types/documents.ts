export type DocumentStatus =
  | 'Uploaded'
  | 'Queued'
  | 'Parsing'
  | 'OcrProcessing'
  | 'Chunking'
  | 'Embedding'
  | 'Indexing'
  | 'Indexed'
  | 'ReindexPending'
  | 'Failed'
  | 'Archived'
  | 'Deleted';

export type UploadDocumentResponse = {
  documentId: string;
  status: DocumentStatus;
  ingestionJobId: string;
  timestampUtc: string;
  createdAtUtc: string;
};

export type DocumentMetadataSuggestion = {
  suggestedTitle: string;
  suggestedCategory?: string | null;
  suggestedCategories: string[];
  suggestedTags: string[];
  strategy: string;
  previewText: string;
};

export type DocumentDetails = {
  documentId: string;
  title: string;
  status: DocumentStatus;
  version: number;
  indexedChunkCount?: number;
  contentType: string;
  source?: string | null;
  lastJobId?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  metadata: {
    category?: string | null;
    tags: string[];
    categories: string[];
    externalId?: string | null;
    accessPolicy?: string | null;
  };
};

export type DocumentEmbeddingInspection = {
  exists: boolean;
  dimensions: number;
  preview: number[];
};

export type DocumentChunkEmbedding = {
  documentId: string;
  chunkId: string;
  dimensions: number;
  values: number[];
};

export type DocumentChunkInspection = {
  chunkId: string;
  chunkIndex: number;
  content: string;
  characterCount: number;
  pageNumber: number;
  endPageNumber?: number | null;
  section?: string | null;
  metadata: Record<string, string>;
  embedding: DocumentEmbeddingInspection;
};

export type DocumentInspection = {
  document: DocumentDetails;
  embeddedChunkCount: number;
  totalChunkCount: number;
  filteredChunkCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  chunks: DocumentChunkInspection[];
};

export type DocumentUploadModel = {
  localId: string;
  conversationSessionId: string;
  fileName: string;
  documentId?: string;
  ingestionJobId?: string;
  status: DocumentStatus | 'sending';
  logicalTitle?: string;
  category?: string;
  tags?: string[];
  error?: string;
  statusMessage?: string;
  details?: DocumentDetails;
};

export type BulkReindexResponse = {
  accepted: boolean;
  jobId: string;
  mode: string;
  documentCount: number;
};
