import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ApiError } from '@/lib/http';
import { useChat } from '@/features/chat/hooks/useChat';

const chatApiMocks = vi.hoisted(() => ({
  getChatSession: vi.fn(),
  sendChatMessage: vi.fn(),
  streamChatMessage: vi.fn()
}));

vi.mock('@/features/chat/api/chatApi', () => chatApiMocks);

const environment = {
  apiBaseUrl: 'http://localhost:5000',
  token: 'dev-token',
  authMode: 'development-headers' as const,
  tenantId: '11111111-1111-1111-1111-111111111111',
  userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  userRole: 'TenantAdmin' as const
};

describe('useChat', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('hidrata o historico da sessao pelo backend', async () => {
    chatApiMocks.getChatSession.mockResolvedValue({
      sessionId: 'session-1',
      tenantId: environment.tenantId,
      userId: environment.userId,
      createdAtUtc: '2026-03-31T22:00:00Z',
      messages: [
        {
          id: 'm-1',
          role: 'assistant',
          content: 'Resposta existente',
          citations: [],
          createdAtUtc: '2026-03-31T22:00:01Z'
        }
      ]
    });

    const { result } = renderHook(() => useChat({ environment, sessionId: 'session-1' }));

    await act(async () => {
      await result.current.hydrateSession();
    });

    expect(result.current.messages).toHaveLength(1);
    expect(result.current.messages[0]?.content).toBe('Resposta existente');
  });

  it('nao consulta o backend ao hidratar com session id vazio', async () => {
    const { result } = renderHook(() => useChat({ environment, sessionId: '' }));

    await act(async () => {
      await result.current.hydrateSession();
    });

    expect(chatApiMocks.getChatSession).not.toHaveBeenCalled();
    expect(result.current.messages).toEqual([]);
  });

  it.each([401, 403, 429])('exibe erro de API para status %i', async (status) => {
    chatApiMocks.sendChatMessage.mockRejectedValue(new ApiError(`Erro ${status}`, status, { message: `Erro ${status}` }));

    const { result } = renderHook(() => useChat({ environment, sessionId: 'session-2' }));

    await act(async () => {
      await result.current.sendMessage({
        message: 'teste',
        templateId: 'grounded_answer',
        templateVersion: '1.0.0',
        documentIds: [],
        useStreaming: false
      });
    });

    expect(result.current.error).toBe(`Erro ${status}`);
  });

  it('usa o session id informado explicitamente no envio', async () => {
    chatApiMocks.sendChatMessage.mockResolvedValue({
      answerId: 'answer-1',
      sessionId: 'session-explicita',
      message: 'Resposta',
      citations: [],
      usage: {
        promptTokens: 1,
        completionTokens: 1,
        totalTokens: 2,
        latencyMs: 10,
        retrievalStrategy: 'hybrid-reranked'
      },
      timestampUtc: '2026-03-31T22:00:01Z'
    });

    const { result } = renderHook(() => useChat({ environment, sessionId: '' }));

    await act(async () => {
      await result.current.sendMessage({
        sessionId: 'session-explicita',
        message: 'teste',
        templateId: 'grounded_answer',
        templateVersion: '1.0.0',
        documentIds: [],
        useStreaming: false
      });
    });

    expect(chatApiMocks.sendChatMessage).toHaveBeenCalledWith(
      environment,
      expect.objectContaining({
        sessionId: 'session-explicita',
        options: expect.objectContaining({ allowGeneralKnowledge: false })
      })
    );
  });

  it('cancela um stream em andamento sem manter estado preso', async () => {
    chatApiMocks.streamChatMessage.mockImplementation(
      async (_env, _request, signal: AbortSignal) =>
        await new Promise<void>((resolve, reject) => {
          signal.addEventListener('abort', () => {
            reject(Object.assign(new Error('AbortError'), { name: 'AbortError' }));
          });
        })
    );

    const { result } = renderHook(() => useChat({ environment, sessionId: 'session-3' }));

    await act(async () => {
      void result.current.sendMessage({
        message: 'stream',
        templateId: 'grounded_answer',
        templateVersion: '1.0.0',
        documentIds: [],
        useStreaming: true
      });
    });

    expect(result.current.isStreaming).toBe(true);

    act(() => {
      result.current.cancelStreaming();
    });

    await waitFor(() => {
      expect(result.current.isStreaming).toBe(false);
    });
  });
});