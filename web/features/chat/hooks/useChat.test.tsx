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

  it.each([401, 403, 429])('exibe erro de API para status %i', async (status) => {
    chatApiMocks.sendChatMessage.mockRejectedValue(new ApiError(`Erro ${status}`, status, { message: `Erro ${status}` }));

    const { result } = renderHook(() => useChat({ environment, sessionId: 'session-2' }));

    await act(async () => {
      await result.current.sendMessage({
        message: 'teste',
        templateId: 'grounded_answer',
        templateVersion: '1.0.0',
        categories: [],
        tags: [],
        useStreaming: false,
        allowGeneralKnowledge: false
      });
    });

    expect(result.current.error).toBe(`Erro ${status}`);
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
        categories: [],
        tags: [],
        useStreaming: true,
        allowGeneralKnowledge: false
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