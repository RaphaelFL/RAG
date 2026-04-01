export type ChatFilters = {
  documentIds?: string[];
  tags?: string[];
  categories?: string[];
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
