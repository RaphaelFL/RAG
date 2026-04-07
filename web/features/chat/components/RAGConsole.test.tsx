import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MessageCard, RAGConsole } from '@/features/chat/components/RAGConsole';

const runtimeEnvironment = {
  apiBaseUrl: 'http://localhost:5000',
  token: 'dev-token',
  authMode: 'development-headers',
  tenantId: '11111111-1111-1111-1111-111111111111',
  userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  userRole: 'TenantUser'
} as const;

const sendMessageMock = vi.fn();
const hydrateSessionMock = vi.fn();
const cancelStreamingMock = vi.fn();
const resetConversationMock = vi.fn();
const suggestUploadMetadataMock = vi.fn();
const submitUploadMock = vi.fn();
const triggerReindexMock = vi.fn();
const triggerTenantFullReindexMock = vi.fn();

const useDocumentUploadsMock = vi.fn(() => ({
  uploads: [],
  error: null,
  lastBulkReindex: null,
  suggestUploadMetadata: suggestUploadMetadataMock,
  submitUpload: submitUploadMock,
  triggerReindex: triggerReindexMock,
  triggerTenantFullReindex: triggerTenantFullReindexMock
}));

vi.mock('@/features/chat/state/useRuntimeEnvironment', () => ({
  useRuntimeEnvironment: () => ({
    environment: runtimeEnvironment,
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
    hydrateSession: hydrateSessionMock,
    sendMessage: sendMessageMock,
    cancelStreaming: cancelStreamingMock,
    resetConversation: resetConversationMock
  })
}));

vi.mock('@/features/documents/hooks/useDocumentUploads', () => ({
  useDocumentUploads: (...args: unknown[]) => useDocumentUploadsMock(...args)
}));

describe('RAGConsole', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    runtimeEnvironment.userRole = 'TenantUser';
    useDocumentUploadsMock.mockReturnValue({
      uploads: [],
      error: null,
      lastBulkReindex: null,
      suggestUploadMetadata: suggestUploadMetadataMock,
      submitUpload: submitUploadMock,
      triggerReindex: triggerReindexMock,
      triggerTenantFullReindex: triggerTenantFullReindexMock
    });
  });

  it('inicia com uma sessao ativa local para TenantUser', async () => {
    render(<RAGConsole />);

    expect(screen.getByText('Upload indisponivel')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Confirmar envio' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nova sessao' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Inspecao documental' })).toHaveAttribute('href', '/inspecao-documental');
    expect(screen.getByRole('link', { name: 'Configuracoes de administrador' })).toHaveAttribute('href', '/configuracoes-de-administrador');
    expect(screen.queryByText(/sessao vinculada:/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Session Id ativo')).not.toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/sessao da conversa permanece interna/i)).toBeInTheDocument();
    });
  });

  it('reinicia a conversa ao solicitar uma nova sessao', () => {
    render(<RAGConsole />);

    fireEvent.click(screen.getByRole('button', { name: 'Nova sessao' }));

    expect(resetConversationMock).toHaveBeenCalledTimes(1);
  });

  it('envia a pergunta usando os documentos indexados sem expor o session id', async () => {
    useDocumentUploadsMock.mockReturnValue({
      uploads: [
        {
          localId: 'local-1',
          conversationSessionId: 'session-qualquer',
          fileName: 'manual.pdf',
          documentId: 'doc-1',
          status: 'Indexed',
          logicalTitle: 'Manual',
          category: 'politicas',
          tags: ['financeiro']
        }
      ],
      error: null,
      lastBulkReindex: null,
      suggestUploadMetadata: suggestUploadMetadataMock,
      submitUpload: submitUploadMock,
      triggerReindex: triggerReindexMock,
      triggerTenantFullReindex: triggerTenantFullReindexMock
    });

    render(<RAGConsole />);

    fireEvent.change(screen.getByLabelText('Pergunta'), {
      target: { value: 'Qual e o status do runtime?' }
    });

    fireEvent.click(screen.getByRole('button', { name: 'Enviar pergunta' }));

    await waitFor(() => {
      expect(sendMessageMock).toHaveBeenCalled();
      expect(sendMessageMock).toHaveBeenCalledWith(expect.objectContaining({
        documentIds: ['doc-1']
      }));
      expect(screen.queryByText(/sessao vinculada:/i)).not.toBeInTheDocument();
    });
  });

  it('preenche e permite editar os campos sugeridos antes do upload', async () => {
    runtimeEnvironment.userRole = 'TenantAdmin';
    suggestUploadMetadataMock.mockResolvedValue({
      suggestedTitle: 'Documento privado',
      suggestedCategory: 'financeiro',
      suggestedCategories: ['financeiro'],
      suggestedTags: ['sigiloso', 'portal'],
      strategy: 'heuristic-direct',
      previewText: 'Conteudo analisado automaticamente.'
    });
    submitUploadMock.mockResolvedValue(undefined);

    const { container } = render(<RAGConsole />);
    const uploadForm = container.querySelector('.upload-form');
    expect(uploadForm).not.toBeNull();
    const uploadQueries = within(uploadForm as HTMLElement);

    fireEvent.change(uploadQueries.getByLabelText('Arquivo'), {
      target: {
        files: [new File(['conteudo'], 'privado.pdf', { type: 'application/pdf' })]
      }
    });

    await waitFor(() => {
      expect(uploadQueries.getByLabelText('Titulo logico')).toHaveValue('Documento privado');
      expect(uploadQueries.getByLabelText('Categoria')).toHaveValue('financeiro');
      expect(uploadQueries.getByLabelText('Tags')).toHaveValue('sigiloso,portal');
    });

    fireEvent.change(uploadQueries.getByLabelText('Categoria'), {
      target: { value: 'politicas' }
    });

    fireEvent.change(uploadQueries.getByLabelText('Tags'), {
      target: { value: 'sigiloso,manual' }
    });

    fireEvent.click(uploadQueries.getByRole('button', { name: 'Confirmar envio' }));

    await waitFor(() => {
      expect(submitUploadMock).toHaveBeenCalledWith(expect.objectContaining({
        title: 'Documento privado',
        category: 'politicas',
        categories: ['politicas'],
        tags: ['sigiloso', 'manual']
      }));
      expect(screen.queryByText(/sessao vinculada:/i)).not.toBeInTheDocument();
    });
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