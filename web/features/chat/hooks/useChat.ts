'use client';

import { startTransition, useMemo, useRef, useState } from 'react';
import { ApiError } from '@/lib/http';
import { getChatSession, sendChatMessage, streamChatMessage } from '@/features/chat/api/chatApi';
import type { RuntimeEnvironment } from '@/types/app';
import type {
  ApiErrorPayload,
  ChatMessageModel,
  ChatRequest,
  ChatSessionSnapshot,
  UsageMetadata
} from '@/features/chat/types/chat';

type UseChatArgs = {
  environment: RuntimeEnvironment;
  sessionId: string;
};

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

    try {
      function updateStreamingMessage(updater: (message: ChatMessageModel) => ChatMessageModel) {
        const activeId = streamingMessageIdRef.current ?? assistantId;
        setMessages((current) => mapMessages(current, (message) => message.id === activeId || Boolean(message.isStreaming), updater));
      }

      await streamChatMessage(environment, request, controller.signal, {
        onStarted: (event) => {
          const previousId = streamingMessageIdRef.current ?? assistantId;
          streamingMessageIdRef.current = event.answerId;
          setMessages((current) =>
            mapMessages(current, (message) => message.id === previousId, (message) => ({
              ...message,
              id: event.answerId
            }))
          );
        },
        onDelta: (event) => {
          updateStreamingMessage((message) => ({
            ...message,
            content: `${message.content}${event.text}`
          }));
        },
        onCitation: (citation) => {
          updateStreamingMessage((message) => ({
            ...message,
            citations: [...message.citations, citation]
          }));
        },
        onCompleted: ({ usage }) => {
          updateStreamingMessage((message) => ({
            ...message,
            usage,
            isStreaming: false
          }));
          setLastUsage(usage);
        },
        onError: (streamError) => {
          setError(streamError.message);
          updateStreamingMessage((message) => ({
            ...message,
            content: message.content || 'Falha ao gerar resposta em streaming.',
            isStreaming: false
          }));
        }
      });
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setError(readableError(error));
      }

      setMessages((current) =>
        current.map((message) =>
          message.isStreaming ? { ...message, isStreaming: false } : message
        )
      );
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
  return session.messages.map((message) => ({
    ...message,
    role: message.role as 'user' | 'assistant'
  }));
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

function isApiErrorPayload(payload: unknown): payload is ApiErrorPayload {
  return typeof payload === 'object' && payload !== null && 'message' in payload;
}
