import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useDocumentUploads } from '@/features/documents/hooks/useDocumentUploads';

const documentApiMocks = vi.hoisted(() => ({
  suggestDocumentMetadata: vi.fn(),
  uploadDocument: vi.fn(),
  getDocument: vi.fn(),
  reindexDocument: vi.fn(),
  bulkReindexDocuments: vi.fn()
}));

vi.mock('@/features/documents/api/documentsApi', () => documentApiMocks);

const environment = {
  apiBaseUrl: 'http://localhost:5000',
  token: 'dev-token',
  authMode: 'development-headers' as const,
  tenantId: '11111111-1111-1111-1111-111111111111',
  userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  userRole: 'TenantAdmin' as const
};

describe('useDocumentUploads', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('retorna sugestoes automaticas de metadados antes do upload', async () => {
    documentApiMocks.suggestDocumentMetadata.mockResolvedValue({
      suggestedTitle: 'Politica de reembolso',
      suggestedCategory: 'financeiro',
      suggestedCategories: ['financeiro'],
      suggestedTags: ['financeiro', 'reembolso'],
      strategy: 'heuristic-direct',
      previewText: 'Politica de reembolso com prazo de 30 dias.'
    });

    const { result } = renderHook(() => useDocumentUploads(environment, 'session-1'));

    let suggestion: Awaited<ReturnType<typeof result.current.suggestUploadMetadata>> | undefined;
    await act(async () => {
      suggestion = await result.current.suggestUploadMetadata(new File(['conteudo'], 'politica.txt', { type: 'text/plain' }));
    });

    expect(suggestion?.suggestedCategory).toBe('financeiro');
    expect(suggestion?.suggestedTags).toContain('reembolso');
  });

  it('mostra o status final do documento apos upload e polling', async () => {
    documentApiMocks.uploadDocument.mockResolvedValue({
      documentId: 'doc-1',
      status: 'Queued',
      ingestionJobId: 'job-1',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
    });

    documentApiMocks.getDocument.mockResolvedValue({
      documentId: 'doc-1',
      title: 'Manual.pdf',
      status: 'Indexed',
      version: 1,
      contentType: 'application/pdf',
      createdAtUtc: '2026-03-31T22:00:00Z',
      updatedAtUtc: '2026-03-31T22:00:10Z',
      metadata: {
        tags: ['financeiro'],
        categories: ['politicas']
      }
    });

    const { result } = renderHook(() => useDocumentUploads(environment, 'session-1'));

    await act(async () => {
      await result.current.submitUpload({
        file: new File(['conteudo'], 'manual.pdf', { type: 'application/pdf' }),
        title: 'Manual',
        category: 'politicas',
        categories: ['politicas'],
        tags: ['financeiro']
      });
    });

    await waitFor(() => {
      expect(result.current.uploads[0]?.status).toBe('Indexed');
      expect(result.current.uploads[0]?.details?.title).toBe('Manual.pdf');
      expect(result.current.uploads[0]?.logicalTitle).toBe('Manual.pdf');
      expect(result.current.uploads[0]?.category).toBe('politicas');
      expect(result.current.uploads[0]?.tags).toEqual(['financeiro']);
    });
  });

  it('escopa o card de uploads pela sessao atual da conversa', async () => {
    documentApiMocks.uploadDocument.mockResolvedValueOnce({
      documentId: 'doc-1',
      status: 'Queued',
      ingestionJobId: 'job-1',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
    });

    documentApiMocks.getDocument.mockResolvedValue({
      documentId: 'doc-1',
      title: 'Manual A',
      status: 'Indexed',
      version: 1,
      contentType: 'application/pdf',
      createdAtUtc: '2026-03-31T22:00:00Z',
      metadata: {
        tags: ['a'],
        categories: ['categoria-a']
      }
    });

    const { result, rerender } = renderHook(
      ({ sessionId }) => useDocumentUploads(environment, sessionId),
      { initialProps: { sessionId: 'session-1' } }
    );

    await act(async () => {
      await result.current.submitUpload({
        file: new File(['conteudo'], 'manual-a.pdf', { type: 'application/pdf' }),
        title: 'Manual A',
        category: 'categoria-a',
        categories: ['categoria-a'],
        tags: ['a']
      });
    });

    await waitFor(() => {
      expect(result.current.uploads).toHaveLength(1);
    });

    rerender({ sessionId: 'session-2' });
    expect(result.current.uploads).toHaveLength(0);

    rerender({ sessionId: 'session-1' });
    expect(result.current.uploads).toHaveLength(1);
  });

  it('enfileira reindex full do tenant e expõe o job retornado', async () => {
    documentApiMocks.bulkReindexDocuments.mockResolvedValue({
      accepted: true,
      jobId: 'bulk-job-1',
      mode: 'full',
      documentCount: 12
    });

    const { result } = renderHook(() => useDocumentUploads(environment, 'session-1'));

    await act(async () => {
      await result.current.triggerTenantFullReindex();
    });

    expect(result.current.lastBulkReindex?.jobId).toBe('bulk-job-1');
    expect(result.current.lastBulkReindex?.documentCount).toBe(12);
  });
});