import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DocumentInspectorDetailConsole from '@/features/documents/components/DocumentInspectorDetailConsole';

const runtimeEnvironment = {
  apiBaseUrl: 'http://localhost:5000',
  token: 'dev-token',
  authMode: 'development-headers',
  tenantId: '11111111-1111-1111-1111-111111111111',
  userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  userRole: 'TenantAdmin'
} as const;

const getDocumentInspectionPageMock = vi.fn();
const getDocumentChunkEmbeddingMock = vi.fn();
const defaultInspectionPage = createInspectionPage();

vi.mock('@/features/chat/state/useRuntimeEnvironment', () => ({
  useRuntimeEnvironment: () => ({
    environment: runtimeEnvironment,
    isReady: true,
    setEnvironment: vi.fn()
  })
}));

vi.mock('@/features/documents/api/documentsApi', () => ({
  getDocumentInspectionPage: (...args: unknown[]) => getDocumentInspectionPageMock(...args),
  getDocumentChunkEmbedding: (...args: unknown[]) => getDocumentChunkEmbeddingMock(...args),
  getDocumentContentUrl: (documentId: string, pageNumber?: number | null) => pageNumber ? `/api/proxy/api/v1/documents/${documentId}/content#page=${pageNumber}` : `/api/proxy/api/v1/documents/${documentId}/content`
}));

function createInspectionPage(options?: {
  chunks?: Array<{
    chunkId: string;
    chunkIndex: number;
    content: string;
    characterCount: number;
    pageNumber: number;
    endPageNumber: number;
    section: string;
    metadata: Record<string, string>;
    embedding: {
      exists: boolean;
      dimensions: number;
      preview: number[];
    };
  }>;
  pageNumber?: number;
  pageSize?: number;
  totalChunkCount?: number;
  filteredChunkCount?: number;
  totalPages?: number;
}) {
  return {
    document: {
      documentId: 'doc-1',
      title: 'Manual Financeiro',
      originalFileName: 'manual-financeiro.txt',
      status: 'Indexed',
      version: 2,
      indexedChunkCount: 3,
      contentType: 'text/plain',
      source: 'frontend-console',
      createdAtUtc: '2026-04-07T10:00:00Z',
      updatedAtUtc: '2026-04-07T10:05:00Z',
      metadata: {
        category: 'financeiro',
        tags: ['reembolso'],
        categories: ['financeiro']
      }
    },
    embeddedChunkCount: 3,
    totalChunkCount: options?.totalChunkCount ?? 3,
    filteredChunkCount: options?.filteredChunkCount ?? options?.chunks?.length ?? 3,
    pageNumber: options?.pageNumber ?? 1,
    pageSize: options?.pageSize ?? 10,
    totalPages: options?.totalPages ?? 1,
    chunks: options?.chunks ?? [
      {
        chunkId: 'doc-1-chunk-0001',
        chunkIndex: 1,
        content: 'O prazo para envio do comprovante e de 5 dias uteis.',
        characterCount: 53,
        pageNumber: 1,
        endPageNumber: 1,
        section: 'Politica de reembolso',
        metadata: {
          chunkingStrategy: 'SlidingWindowChunkingStrategy'
        },
        embedding: {
          exists: true,
          dimensions: 8,
          preview: [0.12, -0.03, 0.44]
        }
      },
      {
        chunkId: 'doc-1-chunk-0002',
        chunkIndex: 2,
        content: 'Despesas acima de 500 reais exigem aprovacao previa.',
        characterCount: 55,
        pageNumber: 1,
        endPageNumber: 1,
        section: 'Aprovacao',
        metadata: {
          chunkingStrategy: 'SlidingWindowChunkingStrategy'
        },
        embedding: {
          exists: true,
          dimensions: 8,
          preview: [0.22, 0.13, -0.18]
        }
      },
      {
        chunkId: 'doc-1-chunk-0003',
        chunkIndex: 3,
        content: 'O reembolso e creditado apos a validacao do financeiro.',
        characterCount: 59,
        pageNumber: 2,
        endPageNumber: 2,
        section: 'Credito',
        metadata: {
          chunkingStrategy: 'SlidingWindowChunkingStrategy'
        },
        embedding: {
          exists: true,
          dimensions: 8,
          preview: [0.04, 0.07, 0.11]
        }
      }
    ]
  };
}

describe('DocumentInspectorDetailConsole', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    getDocumentInspectionPageMock.mockImplementation((
      _environment: typeof runtimeEnvironment,
      _documentId: string,
      input: { search?: string; page?: number; pageSize?: number }
    ) => {
      if (input?.search === 'aprovacao') {
        return Promise.resolve(createInspectionPage({
          filteredChunkCount: 1,
          chunks: [defaultInspectionPage.chunks[1]]
        }));
      }

      if (input?.page === 2 && input?.pageSize === 10) {
        return Promise.resolve(createInspectionPage({
          pageNumber: 2,
          pageSize: 10,
          totalPages: 2,
          totalChunkCount: 11,
          filteredChunkCount: 11,
          chunks: [defaultInspectionPage.chunks[2]]
        }));
      }

      if (input?.pageSize === 10) {
        return Promise.resolve(createInspectionPage({
          pageNumber: 1,
          pageSize: 10,
          totalPages: 2,
          totalChunkCount: 11,
          filteredChunkCount: 11,
          chunks: defaultInspectionPage.chunks.slice(0, 2)
        }));
      }

      return Promise.resolve(createInspectionPage({
        totalChunkCount: 11,
        filteredChunkCount: 11,
        totalPages: 2,
        chunks: defaultInspectionPage.chunks.slice(0, 2)
      }));
    });

    getDocumentChunkEmbeddingMock.mockResolvedValue({
      documentId: 'doc-1',
      chunkId: 'doc-1-chunk-0001',
      dimensions: 4,
      values: [0.123456, -0.654321, 0.777777, 0.111111]
    });
  });

  it('busca os chunks no backend e destaca o termo filtrado', async () => {
    render(<DocumentInspectorDetailConsole documentId="doc-1" />);

    await waitFor(() => {
      expect(getDocumentInspectionPageMock).toHaveBeenCalledWith(runtimeEnvironment, 'doc-1', {
        search: '',
        page: 1,
        pageSize: 10
      });
    });

    fireEvent.change(screen.getByLabelText('Buscar dentro dos chunks'), {
      target: { value: 'aprovacao' }
    });

    await waitFor(() => {
      expect(getDocumentInspectionPageMock).toHaveBeenLastCalledWith(runtimeEnvironment, 'doc-1', {
        search: 'aprovacao',
        page: 1,
        pageSize: 10
      });
      expect(screen.getByText(/despesas acima de 500 reais/i)).toBeInTheDocument();
      expect(screen.queryByText(/prazo para envio do comprovante/i)).not.toBeInTheDocument();
    });

    expect(document.querySelector('mark.chunk-highlight')).not.toBeNull();
  });

  it('navega entre paginas de chunks usando a resposta paginada', async () => {
    render(<DocumentInspectorDetailConsole documentId="doc-1" />);

    await waitFor(() => {
      expect(screen.getByText(/pagina 1 de 2/i)).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: 'Proxima pagina' }));

    await waitFor(() => {
      expect(getDocumentInspectionPageMock).toHaveBeenLastCalledWith(runtimeEnvironment, 'doc-1', {
        search: '',
        page: 2,
        pageSize: 10
      });
      expect(screen.getByText(/creditado apos a validacao do financeiro/i)).toBeInTheDocument();
      expect(screen.getByText(/pagina 2 de 2/i)).toBeInTheDocument();
    });
  });

  it('busca o vetor completo do embedding sob demanda', async () => {
    render(<DocumentInspectorDetailConsole documentId="doc-1" />);

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: 'Mostrar vetor completo' }).length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByRole('button', { name: 'Mostrar vetor completo' })[0]);

    await waitFor(() => {
      expect(getDocumentChunkEmbeddingMock).toHaveBeenCalledWith(runtimeEnvironment, 'doc-1', 'doc-1-chunk-0001');
      expect(screen.getByText(/1\. 0\.123456/i)).toBeInTheDocument();
    });
  });

  it('expoe links para abrir o documento original no topo e por chunk', async () => {
    render(<DocumentInspectorDetailConsole documentId="doc-1" />);

    const originalDocumentLink = await screen.findByRole('link', { name: /ver arquivo original:/i });
    expect(originalDocumentLink).toHaveAttribute('href', '/api/proxy/api/v1/documents/doc-1/content');

    const chunkDocumentLink = screen.getByRole('link', { name: 'Abrir chunk 01 no documento' });
    expect(chunkDocumentLink).toHaveAttribute('href', '/api/proxy/api/v1/documents/doc-1/content#page=1');
  });
});