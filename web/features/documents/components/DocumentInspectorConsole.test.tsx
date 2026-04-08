import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DocumentInspectorConsole from '@/features/documents/components/DocumentInspectorConsole';

const runtimeEnvironment = {
  apiBaseUrl: 'http://localhost:5000',
  token: 'dev-token',
  authMode: 'development-headers',
  tenantId: '11111111-1111-1111-1111-111111111111',
  userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  userRole: 'TenantAdmin'
} as const;

const listDocumentsMock = vi.fn();

vi.mock('@/features/chat/state/useRuntimeEnvironment', () => ({
  useRuntimeEnvironment: () => ({
    environment: runtimeEnvironment,
    isReady: true,
    setEnvironment: vi.fn()
  })
}));

vi.mock('@/features/documents/api/documentsApi', () => ({
  listDocuments: (...args: unknown[]) => listDocumentsMock(...args)
}));

describe('DocumentInspectorConsole', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    listDocumentsMock.mockResolvedValue([
      {
        documentId: 'doc-1',
        title: 'Manual Financeiro',
        originalFileName: 'manual-financeiro.txt',
        status: 'Indexed',
        version: 2,
        indexedChunkCount: 2,
        contentType: 'text/plain',
        source: 'frontend-console',
        createdAtUtc: '2026-04-07T10:00:00Z',
        updatedAtUtc: '2026-04-07T10:05:00Z',
        metadata: {
          category: 'financeiro',
          tags: ['reembolso'],
          categories: ['financeiro']
        }
      }
    ]);
  });

  it('lista documentos e abre a rota de detalhe do documento', async () => {
    render(<DocumentInspectorConsole />);

    await waitFor(() => {
      expect(listDocumentsMock).toHaveBeenCalledTimes(1);
    });

    expect(screen.getByRole('link', { name: /manual financeiro/i })).toHaveAttribute('href', '/inspecao-documental/doc-1');
    expect(screen.getByText('Abrir documento')).toBeInTheDocument();
  });

  it('filtra a lista de documentos pelo texto informado', async () => {
    listDocumentsMock.mockResolvedValue([
      {
        documentId: 'doc-1',
        title: 'Manual Financeiro',
        originalFileName: 'manual-financeiro.txt',
        status: 'Indexed',
        version: 2,
        indexedChunkCount: 2,
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
      {
        documentId: 'doc-2',
        title: 'Politica de RH',
        originalFileName: 'politica-rh.txt',
        status: 'Indexed',
        version: 1,
        indexedChunkCount: 1,
        contentType: 'text/plain',
        source: 'frontend-console',
        createdAtUtc: '2026-04-07T09:00:00Z',
        updatedAtUtc: '2026-04-07T09:05:00Z',
        metadata: {
          category: 'rh',
          tags: ['beneficios'],
          categories: ['rh']
        }
      }
    ]);

    render(<DocumentInspectorConsole />);

    await waitFor(() => {
      expect(listDocumentsMock).toHaveBeenCalledTimes(1);
    });

    fireEvent.change(screen.getByLabelText('Filtrar documentos'), {
      target: { value: 'rh' }
    });

    await waitFor(() => {
      expect(screen.getByRole('link', { name: /politica de rh/i })).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: /manual financeiro/i })).not.toBeInTheDocument();
    });
  });

  it('ordena a lista conforme o criterio selecionado', async () => {
    listDocumentsMock.mockResolvedValue([
      {
        documentId: 'doc-1',
        title: 'Manual Financeiro',
        originalFileName: 'manual-financeiro.txt',
        status: 'Indexed',
        version: 2,
        indexedChunkCount: 2,
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
      {
        documentId: 'doc-2',
        title: 'Politica de RH',
        originalFileName: 'politica-rh.txt',
        status: 'Queued',
        version: 1,
        indexedChunkCount: 1,
        contentType: 'text/plain',
        source: 'frontend-console',
        createdAtUtc: '2026-04-07T09:00:00Z',
        updatedAtUtc: '2026-04-07T09:05:00Z',
        metadata: {
          category: 'rh',
          tags: ['beneficios'],
          categories: ['rh']
        }
      }
    ]);

    render(<DocumentInspectorConsole />);

    await waitFor(() => {
      expect(listDocumentsMock).toHaveBeenCalledTimes(1);
    });

    fireEvent.change(screen.getByLabelText('Ordenar lista'), {
      target: { value: 'title-desc' }
    });

    const documentLinks = screen
      .getAllByRole('link')
      .filter((link) => link.getAttribute('href')?.startsWith('/inspecao-documental/'));

    expect(documentLinks.map((link) => link.getAttribute('href'))).toEqual([
      '/inspecao-documental/doc-2',
      '/inspecao-documental/doc-1'
    ]);
  });
});