export type ChatFilters = {
  documentIds?: string[];
  tags?: string[];
  categories?: string[];
  contentTypes?: string[];
  sources?: string[];
};

export type ChatOptions = {
  maxCitations: number;
  allowGeneralKnowledge: boolean;
  semanticRanking: boolean;
};

export type Citation = {
  documentId: string;
  documentTitle: string;
  chunkId: string;
  snippet: string;
  location?: {
    page?: number | null;
    endPage?: number | null;
    section?: string | null;
  } | null;
  score: number;
};

export type UsageMetadata = {
  model: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  latencyMs: number;
  retrievalStrategy: string;
  runtimeMetrics?: Record<string, number>;
};

export type ChatPolicy = {
  grounded: boolean;
  hadEnoughEvidence: boolean;
  templateId: string;
  templateVersion: string;
};

export type ChatRequest = {
  sessionId: string;
  message: string;
  templateId: string;
  templateVersion: string;
  filters?: ChatFilters;
  options: ChatOptions;
};

export type ChatResponse = {
  answerId: string;
  sessionId: string;
  message: string;
  citations: Citation[];
  usage: UsageMetadata;
  policy: ChatPolicy;
  timestampUtc: string;
};

export type StreamStartedEvent = {
  answerId: string;
  sessionId: string;
};

export type StreamDeltaEvent = {
  text: string;
};

export type StreamErrorEvent = {
  code: string;
  message: string;
  traceId: string;
};

export type ChatMessageModel = {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  citations: Citation[];
  usage?: UsageMetadata;
  createdAtUtc: string;
  isStreaming?: boolean;
};

export type ChatSessionSnapshot = {
  sessionId: string;
  tenantId: string;
  userId: string;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  messages: ChatMessageModel[];
};

export type ApiErrorPayload = {
  code: string;
  message: string;
  details?: Record<string, string[]>;
  traceId?: string;
};

export type RagRuntimeSettings = {
  denseChunkSize: number;
  denseOverlap: number;
  narrativeChunkSize: number;
  narrativeOverlap: number;
  minimumChunkCharacters: number;
  retrievalCandidateMultiplier: number;
  retrievalMaxCandidateCount: number;
  maxContextChunks: number;
  minimumRerankScore: number;
  exactMatchBoost: number;
  titleMatchBoost: number;
  filterMatchBoost: number;
  retrievalCacheTtlSeconds: number;
  chatCompletionCacheTtlSeconds: number;
  embeddingCacheTtlHours: number;
};

export type OperationalAuditCategory = 'retrieval' | 'prompt-assembly' | 'agent-run' | 'tool-execution';

export type OperationalAuditEntry = {
  entryId: string;
  category: OperationalAuditCategory;
  status?: string | null;
  title: string;
  summary: string;
  detailsJson?: string | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
};

export type OperationalAuditFeed = {
  entries: OperationalAuditEntry[];
  nextCursor?: string | null;
};

export type OperationalAuditQuery = {
  category?: string;
  status?: string;
  fromUtc?: string;
  toUtc?: string;
  cursor?: string;
  limit: number;
};
