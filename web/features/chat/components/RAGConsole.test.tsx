import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { MessageCard, RAGConsole } from '@/features/chat/components/RAGConsole';

vi.mock('@/features/chat/state/useRuntimeEnvironment', () => ({
  useRuntimeEnvironment: () => ({
    environment: {
      apiBaseUrl: 'http://localhost:5000',
      token: 'dev-token',
      tenantId: '11111111-1111-1111-1111-111111111111',
      userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      userRole: 'TenantUser'
    },
    isReady: true,
    setEnvironment: vi.fn()
  })
}));

vi.mock('@/features/chat/hooks/useChat', () => ({
  useChat: () => ({
    messages: [],
    assistantMessages: [],
    isLoading: false,
    isStreaming: false,
    error: null,
    lastUsage: null,
    hydrateSession: vi.fn(),
    sendMessage: vi.fn(),
    cancelStreaming: vi.fn(),
    resetConversation: vi.fn()
  })
}));

vi.mock('@/features/documents/hooks/useDocumentUploads', () => ({
  useDocumentUploads: () => ({
    uploads: [],
    error: null,
    submitUpload: vi.fn(),
    triggerReindex: vi.fn()
  })
}));

describe('RAGConsole', () => {
  it('bloqueia upload na UI para TenantUser', () => {
    render(<RAGConsole />);

    expect(screen.getByText('Upload indisponivel')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar documento' })).not.toBeInTheDocument();
  });
});

describe('MessageCard', () => {
  it('sanitiza markdown perigoso antes de renderizar', () => {
    render(
      <MessageCard
        message={{
          id: 'msg-1',
          role: 'assistant',
          content: 'texto seguro <script>alert(1)</script>',
          citations: [],
          createdAtUtc: '2026-03-31T22:00:00Z'
        }}
      />
    );

    expect(screen.getByText(/texto seguro/i)).toBeInTheDocument();
    expect(document.querySelector('script')).toBeNull();
    expect(document.body.innerHTML).not.toContain('<script>alert(1)</script>');
  });
});