import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useDocumentUploads } from '@/features/documents/hooks/useDocumentUploads';

const documentApiMocks = vi.hoisted(() => ({
  uploadDocument: vi.fn(),
  getDocument: vi.fn(),
  reindexDocument: vi.fn()
}));

vi.mock('@/features/documents/api/documentsApi', () => documentApiMocks);

const environment = {
  apiBaseUrl: 'http://localhost:5000',
  token: 'dev-token',
  tenantId: '11111111-1111-1111-1111-111111111111',
  userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  userRole: 'TenantAdmin' as const
};

describe('useDocumentUploads', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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

    const { result } = renderHook(() => useDocumentUploads(environment));

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
    });
  });
});