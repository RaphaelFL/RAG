'use client';

import { startTransition, useMemo, useRef, useState } from 'react';
import type { Dispatch, RefObject, SetStateAction } from 'react';
import { ApiError } from '@/lib/http';
import { getChatSession, sendChatMessage, streamChatMessage } from '@/features/chat/api/chatApi';
import type { RuntimeEnvironment } from '@/types/app';
import type {
  ApiErrorPayload,
  ChatMessageModel,
  ChatRequest,
  ChatSessionSnapshot,
  Citation,
  StreamErrorEvent,
  StreamStartedEvent,
  UsageMetadata
} from '@/features/chat/types/chat';

type UseChatArgs = {
  environment: RuntimeEnvironment;
  sessionId: string;
};

type MessageSetter = Dispatch<SetStateAction<ChatMessageModel[]>>;
type NullableStringRef = RefObject<string | null>;

export function useChat({ environment, sessionId }: UseChatArgs) {
  const [messages, setMessages] = useState<ChatMessageModel[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUsage, setLastUsage] = useState<UsageMetadata | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const streamingMessageIdRef = useRef<string | null>(null);

  const assistantMessages = useMemo(
    () => messages.filter((message) => message.role === 'assistant'),
    [messages]
  );

  async function hydrateSession() {
    setError(null);

    try {
      const session = await getChatSession(environment, sessionId);
      startTransition(() => {
        setMessages(normalizeSession(session));
      });
      return session;
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        startTransition(() => {
          setMessages([]);
        });
        return null;
      }

      setError(readableError(error));
      throw error;
    }
  }

  async function sendMessage(input: {
    message: string;
    templateId: string;
    templateVersion: string;
    categories: string[];
    tags: string[];
    useStreaming: boolean;
    allowGeneralKnowledge: boolean;
  }) {
    const userEntry: ChatMessageModel = {
      id: crypto.randomUUID(),
      role: 'user',
      content: input.message,
      citations: [],
      createdAtUtc: new Date().toISOString()
    };

    setError(null);
    setMessages((current) => [...current, userEntry]);

    const request: ChatRequest = {
      sessionId,
      message: input.message,
      templateId: input.templateId,
      templateVersion: input.templateVersion,
      filters: {
        categories: input.categories,
        tags: input.tags
      },
      options: {
        maxCitations: 5,
        allowGeneralKnowledge: input.allowGeneralKnowledge,
        semanticRanking: true
      }
    };

    if (input.useStreaming) {
      return stream(request);
    }

    setIsLoading(true);

    try {
      const response = await sendChatMessage(environment, request);
      const assistantEntry: ChatMessageModel = {
        id: response.answerId,
        role: 'assistant',
        content: response.message,
        citations: response.citations,
        usage: response.usage,
        createdAtUtc: response.timestampUtc
      };

      setMessages((current) => [...current, assistantEntry]);
      setLastUsage(response.usage);
    } catch (error) {
      setError(readableError(error));
    } finally {
      setIsLoading(false);
    }
  }

  async function stream(request: ChatRequest) {
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    const assistantId = crypto.randomUUID();
    streamingMessageIdRef.current = assistantId;
    setIsStreaming(true);
    appendStreamingPlaceholder(setMessages, assistantId);

    try {
      const handlers = createStreamHandlers({
        assistantId,
        setMessages,
        setError,
        setLastUsage,
        streamingMessageIdRef
      });

      await streamChatMessage(environment, request, controller.signal, handlers);
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setError(readableError(error));
      }

      clearStreamingFlags(setMessages);
    } finally {
      setIsStreaming(false);
      abortControllerRef.current = null;
      streamingMessageIdRef.current = null;
    }
  }

  function cancelStreaming() {
    abortControllerRef.current?.abort();
  }

  function resetConversation() {
    setMessages([]);
    setError(null);
    setLastUsage(null);
  }

  return {
    messages,
    assistantMessages,
    isLoading,
    isStreaming,
    error,
    lastUsage,
    hydrateSession,
    sendMessage,
    cancelStreaming,
    resetConversation
  };
}

function normalizeSession(session: ChatSessionSnapshot) {
  return session.messages;
}

function readableError(error: unknown) {
  if (error instanceof ApiError) {
    const payload = isApiErrorPayload(error.payload) ? error.payload : undefined;
    return payload?.message ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'Erro inesperado.';
}

function mapMessages(
  messages: ChatMessageModel[],
  predicate: (message: ChatMessageModel) => boolean,
  updater: (message: ChatMessageModel) => ChatMessageModel
) {
  return messages.map((message) => (predicate(message) ? updater(message) : message));
}

function appendStreamingPlaceholder(setMessages: MessageSetter, assistantId: string) {
  setMessages((current) => [
    ...current,
    {
      id: assistantId,
      role: 'assistant',
      content: '',
      citations: [],
      createdAtUtc: new Date().toISOString(),
      isStreaming: true
    }
  ]);
}

function updateStreamingMessage(
  setMessages: MessageSetter,
  streamingMessageIdRef: NullableStringRef,
  fallbackId: string,
  updater: (message: ChatMessageModel) => ChatMessageModel
) {
  const activeId = streamingMessageIdRef.current ?? fallbackId;
  setMessages((current) => mapMessages(current, (message) => message.id === activeId || Boolean(message.isStreaming), updater));
}

function renameStreamingMessage(
  setMessages: MessageSetter,
  streamingMessageIdRef: NullableStringRef,
  fallbackId: string,
  event: StreamStartedEvent
) {
  const previousId = streamingMessageIdRef.current ?? fallbackId;
  streamingMessageIdRef.current = event.answerId;
  setMessages((current) =>
    mapMessages(current, (message) => message.id === previousId, (message) => ({
      ...message,
      id: event.answerId
    }))
  );
}

function completeStreamingMessage(
  setMessages: MessageSetter,
  streamingMessageIdRef: NullableStringRef,
  fallbackId: string,
  usage: UsageMetadata
) {
  updateStreamingMessage(setMessages, streamingMessageIdRef, fallbackId, (message) => ({
    ...message,
    usage,
    isStreaming: false
  }));
}

function failStreamingMessage(
  setMessages: MessageSetter,
  streamingMessageIdRef: NullableStringRef,
  fallbackId: string
) {
  updateStreamingMessage(setMessages, streamingMessageIdRef, fallbackId, (message) => ({
    ...message,
    content: message.content || 'Falha ao gerar resposta em streaming.',
    isStreaming: false
  }));
}

function clearStreamingFlags(setMessages: MessageSetter) {
  setMessages((current) => current.map((message) => (message.isStreaming ? { ...message, isStreaming: false } : message)));
}

function createStreamHandlers(args: {
  assistantId: string;
  setMessages: MessageSetter;
  setError: Dispatch<SetStateAction<string | null>>;
  setLastUsage: Dispatch<SetStateAction<UsageMetadata | null>>;
  streamingMessageIdRef: NullableStringRef;
}) {
  const { assistantId, setMessages, setError, setLastUsage, streamingMessageIdRef } = args;

  return {
    onStarted(event: StreamStartedEvent) {
      renameStreamingMessage(setMessages, streamingMessageIdRef, assistantId, event);
    },
    onDelta(event: { text: string }) {
      updateStreamingMessage(setMessages, streamingMessageIdRef, assistantId, (message) => ({
        ...message,
        content: `${message.content}${event.text}`
      }));
    },
    onCitation(citation: Citation) {
      updateStreamingMessage(setMessages, streamingMessageIdRef, assistantId, (message) => ({
        ...message,
        citations: [...message.citations, citation]
      }));
    },
    onCompleted({ usage }: { usage: UsageMetadata }) {
      completeStreamingMessage(setMessages, streamingMessageIdRef, assistantId, usage);
      setLastUsage(usage);
    },
    onError(streamError: StreamErrorEvent) {
      setError(streamError.message);
      failStreamingMessage(setMessages, streamingMessageIdRef, assistantId);
    }
  };
}

function isApiErrorPayload(payload: unknown): payload is ApiErrorPayload {
  return typeof payload === 'object' && payload !== null && 'message' in payload;
}
