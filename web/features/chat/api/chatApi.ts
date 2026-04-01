import { apiRequest, buildHeaders, buildUrl, ApiError } from '@/lib/http';
import { readServerSentEvents } from '@/lib/sse';
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
  const response = await fetch(buildUrl(env.apiBaseUrl, '/api/v1/chat/stream'), {
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
    const parsed = JSON.parse(entry.data);

    if (entry.event === 'started') {
      handlers.onStarted(parsed.data ?? parsed);
      continue;
    }

    if (entry.event === 'delta') {
      handlers.onDelta(parsed.data ?? parsed);
      continue;
    }

    if (entry.event === 'citation') {
      handlers.onCitation(parsed.data ?? parsed);
      continue;
    }

    if (entry.event === 'completed') {
      handlers.onCompleted(parsed.data ?? parsed);
      continue;
    }

    if (entry.event === 'error') {
      handlers.onError(parsed.data ?? parsed);
    }
  }
}
