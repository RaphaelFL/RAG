import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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
    vi.useRealTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
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

  it('mantem o status inicial retornado pelo backend logo apos o upload', async () => {
    documentApiMocks.uploadDocument.mockResolvedValue({
      documentId: 'doc-1',
      status: 'Queued',
      ingestionJobId: 'job-1',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
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
      expect(result.current.uploads[0]?.status).toBe('Queued');
      expect(result.current.uploads[0]?.details).toBeUndefined();
      expect(result.current.uploads[0]?.logicalTitle).toBe('Manual');
      expect(result.current.uploads[0]?.category).toBe('politicas');
      expect(result.current.uploads[0]?.tags).toEqual(['financeiro']);
    });

    expect(documentApiMocks.getDocument).not.toHaveBeenCalled();
  });

  it('escopa o card de uploads pela sessao atual da conversa', async () => {
    documentApiMocks.uploadDocument.mockResolvedValueOnce({
      documentId: 'doc-1',
      status: 'Queued',
      ingestionJobId: 'job-1',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
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

  it('atualiza o status automaticamente apos upload', async () => {
    vi.useFakeTimers();

    documentApiMocks.uploadDocument.mockResolvedValue({
      documentId: 'doc-sem-polling',
      status: 'Queued',
      ingestionJobId: 'job-sem-polling',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
    });

    documentApiMocks.getDocument.mockResolvedValueOnce({
      documentId: 'doc-sem-polling',
      title: 'Planilha.xlsx',
      originalFileName: 'planilha.xlsx',
      status: 'Indexed',
      version: 1,
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      createdAtUtc: '2026-03-31T22:00:00Z',
      updatedAtUtc: '2026-03-31T22:05:00Z',
      metadata: {
        tags: ['financeiro'],
        categories: ['planilhas']
      }
    });

    const { result } = renderHook(() => useDocumentUploads(environment, 'session-1'));

    await act(async () => {
      await result.current.submitUpload({
        file: new File(['conteudo'], 'planilha.xlsx', {
          type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        }),
        title: 'Planilha',
        category: 'planilhas',
        categories: ['planilhas'],
        tags: ['financeiro']
      });
    });

    expect(result.current.error).toBeNull();
    expect(result.current.uploads[0]?.status).toBe('Queued');
    expect(result.current.uploads[0]?.statusMessage).toContain('sera atualizado automaticamente');

    await act(async () => {
      await vi.advanceTimersByTimeAsync(8000);
    });

    expect(result.current.uploads[0]?.status).toBe('Indexed');
    expect(result.current.uploads[0]?.details?.title).toBe('Planilha.xlsx');

    expect(documentApiMocks.getDocument).toHaveBeenCalledTimes(1);
  });

  it('atualiza o status automaticamente apos reindex', async () => {
    vi.useFakeTimers();

    documentApiMocks.uploadDocument.mockResolvedValue({
      documentId: 'doc-1',
      status: 'Queued',
      ingestionJobId: 'job-1',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
    });

    documentApiMocks.reindexDocument.mockResolvedValue({
      documentId: 'doc-1',
      status: 'ReindexPending',
      chunksReindexed: 0,
      jobId: 'job-reindex'
    });

    documentApiMocks.getDocument.mockResolvedValueOnce({
      documentId: 'doc-1',
      title: 'Manual.pdf',
      originalFileName: 'manual.pdf',
      status: 'Indexed',
      version: 2,
      contentType: 'application/pdf',
      createdAtUtc: '2026-03-31T22:00:00Z',
      updatedAtUtc: '2026-03-31T22:10:00Z',
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

    await act(async () => {
      await result.current.triggerReindex('doc-1', false);
    });

    expect(result.current.uploads[0]?.status).toBe('ReindexPending');
    expect(result.current.uploads[0]?.ingestionJobId).toBe('job-reindex');
    expect(result.current.uploads[0]?.statusMessage).toContain('sera atualizado automaticamente');

    await act(async () => {
      await vi.advanceTimersByTimeAsync(8000);
    });

    expect(result.current.uploads[0]?.status).toBe('Indexed');
    expect(result.current.uploads[0]?.details?.version).toBe(2);

    expect(documentApiMocks.getDocument).toHaveBeenCalledTimes(1);
  });

  it('continua tentando automaticamente de forma espaçada enquanto o documento segue pendente', async () => {
    vi.useFakeTimers();

    documentApiMocks.uploadDocument.mockResolvedValue({
      documentId: 'doc-refresh',
      status: 'Queued',
      ingestionJobId: 'job-refresh',
      timestampUtc: '2026-03-31T22:00:00Z',
      createdAtUtc: '2026-03-31T22:00:00Z'
    });

    documentApiMocks.getDocument
      .mockResolvedValueOnce({
        documentId: 'doc-refresh',
        title: 'Manual atualizado.pdf',
        originalFileName: 'manual-atualizado.pdf',
        status: 'Queued',
        version: 1,
        contentType: 'application/pdf',
        createdAtUtc: '2026-03-31T22:00:00Z',
        metadata: {
          tags: ['financeiro'],
          categories: ['politicas']
        }
      })
      .mockResolvedValueOnce({
        documentId: 'doc-refresh',
        title: 'Manual atualizado.pdf',
        originalFileName: 'manual-atualizado.pdf',
        status: 'Indexed',
        version: 2,
        contentType: 'application/pdf',
        createdAtUtc: '2026-03-31T22:00:00Z',
        updatedAtUtc: '2026-03-31T22:10:00Z',
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

    await act(async () => {
      await vi.advanceTimersByTimeAsync(8000);
    });

    expect(result.current.uploads[0]?.status).toBe('Queued');

    await act(async () => {
      await vi.advanceTimersByTimeAsync(12000);
    });

    expect(result.current.uploads[0]?.status).toBe('Indexed');
    expect(result.current.uploads[0]?.details?.version).toBe(2);

    expect(documentApiMocks.getDocument).toHaveBeenCalledTimes(2);
  });
});