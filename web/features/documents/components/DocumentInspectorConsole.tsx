'use client';

import * as React from 'react';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { listDocuments } from '@/features/documents/api/documentsApi';
import type { DocumentDetails, DocumentStatus } from '@/features/documents/types/documents';

const ACTIVE_DOCUMENT_STATUSES = new Set<DocumentStatus>(['Queued', 'Parsing', 'OcrProcessing', 'Chunking', 'Embedding', 'Indexing', 'ReindexPending']);
type DocumentSortKey = 'updated-desc' | 'updated-asc' | 'title-asc' | 'title-desc' | 'status';

export default function DocumentInspectorConsole() {
  const { useDeferredValue, useMemo, useState } = React;
  const { environment, isReady } = useRuntimeEnvironment();
  const [documents, setDocuments] = useState<DocumentDetails[]>([]);
  const [filter, setFilter] = useState('');
  const [sortKey, setSortKey] = useState<DocumentSortKey>('updated-desc');
  const [refreshKey, setRefreshKey] = useState(0);
  const [documentsError, setDocumentsError] = useState<string | null>(null);
  const [isDocumentsLoading, setIsDocumentsLoading] = useState(false);
  const deferredFilter = useDeferredValue(filter);

  React.useEffect(() => {
    if (!isReady) {
      return;
    }

    let cancelled = false;

    async function loadDocuments() {
      setIsDocumentsLoading(true);
      setDocumentsError(null);

      try {
        const payload = await listDocuments(environment);
        if (cancelled) {
          return;
        }

        setDocuments(payload);
      } catch (error) {
        if (!cancelled) {
          setDocumentsError(readableClientError(error));
          setDocuments([]);
        }
      } finally {
        if (!cancelled) {
          setIsDocumentsLoading(false);
        }
      }
    }

    loadDocuments();

    return () => {
      cancelled = true;
    };
  }, [environment, isReady, refreshKey]);

  React.useEffect(() => {
    if (!documents.some((document) => ACTIVE_DOCUMENT_STATUSES.has(document.status))) {
      return;
    }

    const timerId = globalThis.setInterval(() => {
      setRefreshKey((current) => current + 1);
    }, 5000);

    return () => {
      globalThis.clearInterval(timerId);
    };
  }, [documents]);

  const filteredDocuments = useMemo(() => {
    const normalizedFilter = deferredFilter.trim().toLowerCase();
    if (!normalizedFilter) {
      return [...documents].sort((left, right) => compareDocuments(left, right, sortKey));
    }

    const filtered = documents.filter((document) => {
      const haystack = [
        document.title,
        document.metadata.category,
        document.contentType,
        document.source,
        ...(document.metadata.tags ?? []),
        ...(document.metadata.categories ?? [])
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      return haystack.includes(normalizedFilter);
    });

    return [...filtered].sort((left, right) => compareDocuments(left, right, sortKey));
  }, [deferredFilter, documents, sortKey]);

  const totalIndexedChunks = useMemo(
    () => documents.reduce((sum, document) => sum + (document.indexedChunkCount ?? 0), 0),
    [documents]
  );
  const activeDocumentsCount = useMemo(
    () => documents.filter((document) => ACTIVE_DOCUMENT_STATUSES.has(document.status)).length,
    [documents]
  );

  function handleRefresh() {
    setRefreshKey((current) => current + 1);
  }

  return (
    <main className="console-shell">
      <section className="console-hero">
        <p className="console-kicker">Inspecao persistida</p>
        <h1>Documentos enviados</h1>
        <p>
          Entre na lista de documentos enviados, escolha um item e abra a pagina dele para ver
          os chunks gerados e os embeddings persistidos.
        </p>
        <div className="hero-actions">
          <a className="button ghost" href="/">
            Voltar ao console
          </a>
          <a className="button secondary" href="/configuracoes-de-administrador">
            Configuracoes de administrador
          </a>
          <button className="button secondary" onClick={handleRefresh} type="button">
            Atualizar painel
          </button>
        </div>
      </section>

      {activeDocumentsCount > 0 ? (
        <div className="info-banner">
          Atualizacao automatica ativa para {activeDocumentsCount} documento(s) ainda em processamento.
        </div>
      ) : null}

      <section className="document-list-shell panel">
        <div className="panel-header">
          <div>
            <h2>Catalogo acessivel</h2>
            <p>{documents.length} documento(s) visiveis, {totalIndexedChunks} chunk(s) indexado(s).</p>
          </div>
          <span className={clsx('badge', isDocumentsLoading ? 'badge-warning' : 'badge-success')}>
            {isDocumentsLoading ? 'Carregando' : 'Sincronizado'}
          </span>
        </div>

        {documentsError ? <div className="error-banner">{documentsError}</div> : null}

        <label>
          <span>Filtrar documentos</span>
          <input
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
            placeholder="Titulo, categoria, tag ou origem"
          />
        </label>

        <label>
          <span>Ordenar lista</span>
          <select value={sortKey} onChange={(event) => setSortKey(event.target.value as DocumentSortKey)}>
            <option value="updated-desc">Mais recentes primeiro</option>
            <option value="updated-asc">Mais antigos primeiro</option>
            <option value="title-asc">Titulo A-Z</option>
            <option value="title-desc">Titulo Z-A</option>
            <option value="status">Por status</option>
          </select>
        </label>

        {filteredDocuments.length === 0 ? (
          <div className="empty-state compact">
            <h3>Nenhum documento encontrado</h3>
            <p>Ajuste o filtro ou envie um novo documento para comecar a inspecao.</p>
          </div>
        ) : (
          <div className="document-browser-list document-browser-list-wide">
            {filteredDocuments.map((document) => (
              <a
                key={document.documentId}
                className="document-browser-entry document-browser-entry-link"
                href={`/inspecao-documental/${document.documentId}`}
              >
                <div className="document-browser-entry-header">
                  <strong>{document.title}</strong>
                  <span className={clsx('badge', getStatusColor(document.status))}>{getStatusLabel(document.status)}</span>
                </div>
                <p>{document.metadata.category ?? 'Sem categoria'} • {document.contentType}</p>
                <div className="document-browser-meta">
                  <span>{document.indexedChunkCount ?? 0} chunk(s)</span>
                  <span>{formatDateTime(document.updatedAtUtc ?? document.createdAtUtc)}</span>
                  {document.source ? <span>{document.source}</span> : null}
                </div>
                <span className="document-browser-link-hint">Abrir documento</span>
              </a>
            ))}
          </div>
        )}
      </section>
    </main>
  );
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString('pt-BR');
}

function compareDocuments(left: DocumentDetails, right: DocumentDetails, sortKey: DocumentSortKey) {
  switch (sortKey) {
    case 'updated-asc':
      return new Date(left.updatedAtUtc ?? left.createdAtUtc).getTime() - new Date(right.updatedAtUtc ?? right.createdAtUtc).getTime();
    case 'title-asc':
      return left.title.localeCompare(right.title, 'pt-BR');
    case 'title-desc':
      return right.title.localeCompare(left.title, 'pt-BR');
    case 'status':
      return left.status.localeCompare(right.status, 'pt-BR');
    case 'updated-desc':
    default:
      return new Date(right.updatedAtUtc ?? right.createdAtUtc).getTime() - new Date(left.updatedAtUtc ?? left.createdAtUtc).getTime();
  }
}

function getStatusColor(status: DocumentStatus) {
  switch (status) {
    case 'Queued':
    case 'ReindexPending':
      return 'badge-warning';
    case 'Parsing':
    case 'OcrProcessing':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
      return 'badge-accent';
    case 'Indexed':
      return 'badge-success';
    case 'Failed':
      return 'badge-danger';
    default:
      return 'badge-neutral';
  }
}

function getStatusLabel(status: DocumentStatus) {
  switch (status) {
    case 'Queued':
      return 'Na fila';
    case 'Parsing':
      return 'Extraindo texto';
    case 'OcrProcessing':
      return 'OCR';
    case 'Chunking':
      return 'Chunking';
    case 'Embedding':
      return 'Embedding';
    case 'Indexing':
      return 'Indexando';
    case 'Indexed':
      return 'Indexado';
    case 'ReindexPending':
      return 'Reindex pendente';
    case 'Failed':
      return 'Falhou';
    default:
      return status;
  }
}

function readableClientError(error: unknown) {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'Erro inesperado ao consultar a inspecao documental.';
}