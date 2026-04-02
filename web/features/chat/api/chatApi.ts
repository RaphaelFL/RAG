import { apiRequest, buildHeaders, ApiError } from '@/lib/http';
import { readServerSentEvents } from '@/lib/sse';
import { buildProxyUrl } from '@/lib/runtimeEnvironment';
import type { RuntimeEnvironment } from '@/types/app';
import type {
  ChatRequest,
  ChatResponse,
  ChatSessionSnapshot,
  Citation,
  StreamDeltaEvent,
  StreamErrorEvent,
  StreamStartedEvent,
  UsageMetadata
} from '@/features/chat/types/chat';

export async function sendChatMessage(env: RuntimeEnvironment, request: ChatRequest) {
  return apiRequest<ChatResponse>(env, '/api/v1/chat/message', {
    method: 'POST',
    jsonBody: request
  });
}

export async function getChatSession(env: RuntimeEnvironment, sessionId: string) {
  return apiRequest<ChatSessionSnapshot>(env, `/api/v1/chat/sessions/${sessionId}`, {
    method: 'GET'
  });
}

export async function streamChatMessage(
  env: RuntimeEnvironment,
  request: ChatRequest,
  signal: AbortSignal,
  handlers: {
    onStarted: (event: StreamStartedEvent) => void;
    onDelta: (event: StreamDeltaEvent) => void;
    onCitation: (citation: Citation) => void;
    onCompleted: (payload: { usage: UsageMetadata }) => void;
    onError: (error: StreamErrorEvent) => void;
  }
) {
  const response = await fetch(buildProxyUrl('/api/v1/chat/stream'), {
    method: 'POST',
    headers: buildHeaders(env),
    body: JSON.stringify(request),
    signal
  });

  if (!response.ok || !response.body) {
    const payload = await response.json().catch(() => undefined);
    throw new ApiError(
      typeof payload?.message === 'string' ? payload.message : `HTTP ${response.status}`,
      response.status,
      payload
    );
  }

  for await (const entry of readServerSentEvents(response.body)) {
    const parsed = normalizeStreamPayload(JSON.parse(entry.data));
    const payload = parsed.data ?? parsed;

    if (entry.event === 'started') {
      if (isStreamStartedEvent(payload)) {
        handlers.onStarted(payload);
      }
      continue;
    }

    if (entry.event === 'delta') {
      if (isStreamDeltaEvent(payload)) {
        handlers.onDelta(payload);
      }
      continue;
    }

    if (entry.event === 'citation') {
      if (isCitation(payload)) {
        handlers.onCitation(payload);
      }
      continue;
    }

    if (entry.event === 'completed') {
      if (hasUsagePayload(payload)) {
        handlers.onCompleted(payload);
      }
      continue;
    }

    if (entry.event === 'error') {
      if (isStreamErrorEvent(payload)) {
        handlers.onError(payload);
      }
    }
  }
}

function normalizeStreamPayload(payload: unknown): Record<string, unknown> {
  if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
    return {};
  }

  const record = payload as Record<string, unknown>;
  const normalizedData = normalizeRecord(record.data ?? record.Data);
  if (normalizedData) {
    return { data: normalizedData };
  }

  return normalizeRecord(record) ?? {};
}

function normalizeRecord(value: unknown): Record<string, unknown> | null {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return null;
  }

  const output: Record<string, unknown> = {};
  for (const [key, entry] of Object.entries(value)) {
    const normalizedKey = key.length > 0 ? `${key[0].toLowerCase()}${key.slice(1)}` : key;
    output[normalizedKey] = Array.isArray(entry)
      ? entry.map((item) => normalizeRecord(item) ?? item)
      : normalizeRecord(entry) ?? entry;
  }

  return output;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function isStreamStartedEvent(value: unknown): value is StreamStartedEvent {
  return isRecord(value)
    && typeof value.answerId === 'string'
    && typeof value.sessionId === 'string';
}

function isStreamDeltaEvent(value: unknown): value is StreamDeltaEvent {
  return isRecord(value) && typeof value.text === 'string';
}

function isStreamErrorEvent(value: unknown): value is StreamErrorEvent {
  return isRecord(value)
    && typeof value.code === 'string'
    && typeof value.message === 'string'
    && typeof value.traceId === 'string';
}

function isCitation(value: unknown): value is Citation {
  return isRecord(value)
    && typeof value.documentId === 'string'
    && typeof value.documentTitle === 'string'
    && typeof value.chunkId === 'string'
    && typeof value.snippet === 'string'
    && typeof value.score === 'number';
}

function hasUsagePayload(value: unknown): value is { usage: UsageMetadata } {
  return isRecord(value) && isUsageMetadata(value.usage);
}

function isUsageMetadata(value: unknown): value is UsageMetadata {
  return isRecord(value)
    && typeof value.model === 'string'
    && typeof value.promptTokens === 'number'
    && typeof value.completionTokens === 'number'
    && typeof value.totalTokens === 'number'
    && typeof value.latencyMs === 'number'
    && typeof value.retrievalStrategy === 'string';
}
