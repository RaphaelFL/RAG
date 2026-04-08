'use client';

import * as React from 'react';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { getDocumentChunkEmbedding, getDocumentContentUrl, getDocumentInspectionPage } from '@/features/documents/api/documentsApi';
import type { DocumentChunkEmbedding, DocumentChunkInspection, DocumentInspection, DocumentStatus } from '@/features/documents/types/documents';

const ACTIVE_DOCUMENT_STATUSES = new Set<DocumentStatus>(['Queued', 'Parsing', 'OcrProcessing', 'Chunking', 'Embedding', 'Indexing', 'ReindexPending']);
const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

export default function DocumentInspectorDetailConsole({ documentId }: Readonly<{ documentId: string }>) {
  const { useDeferredValue, useState } = React;
  const { environment, isReady } = useRuntimeEnvironment();
  const [inspection, setInspection] = useState<DocumentInspection | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [chunkSearch, setChunkSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [inspectionError, setInspectionError] = useState<string | null>(null);
  const [isInspectionLoading, setIsInspectionLoading] = useState(false);
  const [expandedEmbeddings, setExpandedEmbeddings] = useState<Record<string, boolean>>({});
  const [fullEmbeddings, setFullEmbeddings] = useState<Record<string, DocumentChunkEmbedding>>({});
  const [embeddingErrors, setEmbeddingErrors] = useState<Record<string, string>>({});
  const [loadingEmbeddings, setLoadingEmbeddings] = useState<Record<string, boolean>>({});
  const deferredChunkSearch = useDeferredValue(chunkSearch);

  React.useEffect(() => {
    if (!isReady) {
      return;
    }

    let cancelled = false;

    async function loadInspection() {
      setIsInspectionLoading(true);
      setInspectionError(null);

      try {
        const payload = await getDocumentInspectionPage(environment, documentId, {
          search: deferredChunkSearch,
          page,
          pageSize
        });
        if (!cancelled) {
          setInspection(payload);
        }
      } catch (error) {
        if (!cancelled) {
          setInspection(null);
          setInspectionError(readableClientError(error));
        }
      } finally {
        if (!cancelled) {
          setIsInspectionLoading(false);
        }
      }
    }

    loadInspection();

    return () => {
      cancelled = true;
    };
  }, [deferredChunkSearch, documentId, environment, isReady, page, pageSize, refreshKey]);

  const shouldAutoRefresh = Boolean(inspection && ACTIVE_DOCUMENT_STATUSES.has(inspection.document.status));

  React.useEffect(() => {
    if (!shouldAutoRefresh) {
      return;
    }

    const timerId = globalThis.setInterval(() => {
      setRefreshKey((current) => current + 1);
    }, 5000);

    return () => {
      globalThis.clearInterval(timerId);
    };
  }, [shouldAutoRefresh]);

  React.useEffect(() => {
    setPage(1);
  }, [deferredChunkSearch, pageSize, documentId]);

  async function handleToggleEmbedding(chunk: DocumentChunkInspection) {
    if (!chunk.embedding.exists) {
      return;
    }

    if (fullEmbeddings[chunk.chunkId]) {
      setExpandedEmbeddings((current) => ({
        ...current,
        [chunk.chunkId]: !current[chunk.chunkId]
      }));
      return;
    }

    setLoadingEmbeddings((current) => ({ ...current, [chunk.chunkId]: true }));
    setEmbeddingErrors((current) => ({ ...current, [chunk.chunkId]: '' }));

    try {
      const embedding = await getDocumentChunkEmbedding(environment, documentId, chunk.chunkId);
      setFullEmbeddings((current) => ({ ...current, [chunk.chunkId]: embedding }));
      setExpandedEmbeddings((current) => ({ ...current, [chunk.chunkId]: true }));
    } catch (error) {
      setEmbeddingErrors((current) => ({
        ...current,
        [chunk.chunkId]: readableClientError(error)
      }));
    } finally {
      setLoadingEmbeddings((current) => ({ ...current, [chunk.chunkId]: false }));
    }
  }

  function handleRefresh() {
    setRefreshKey((current) => current + 1);
  }

  const document = inspection?.document ?? null;

  return (
    <main className="console-shell">
      <section className="console-hero">
        <p className="console-kicker">Documento enviado</p>
        <h1>{document?.title ?? 'Inspecao documental'}</h1>
        <p>
          Abra o documento enviado e acompanhe os chunks gerados, o resumo dos embeddings e o vetor completo apenas quando precisar inspecionar um chunk especifico.
        </p>
        <div className="hero-actions">
          <a className="button ghost" href="/inspecao-documental">
            Voltar para todos os documentos
          </a>
          {document ? (
            <a className="button ghost" href={getDocumentContentUrl(document.documentId)} rel="noreferrer" target="_blank">
              Abrir documento original
            </a>
          ) : null}
          <button className="button secondary" onClick={handleRefresh} type="button">
            Atualizar documento
          </button>
        </div>
      </section>

      {shouldAutoRefresh ? (
        <div className="info-banner">
          Atualizacao automatica ativa enquanto o documento estiver em {getStatusLabel(document?.status ?? 'Queued')}.
        </div>
      ) : null}

      <section className="document-detail-shell panel">
        <div className="panel-header">
          <div>
            <h2>Detalhe do documento</h2>
            <p>
              {document
                ? 'Veja os chunks feitos, o progresso do processamento e abra o vetor completo de embedding por chunk.'
                : 'Carregando documento selecionado.'}
            </p>
          </div>
          <span className={clsx('badge', isInspectionLoading ? 'badge-warning' : 'badge-accent')}>
            {isInspectionLoading ? 'Atualizando' : 'Detalhe'}
          </span>
        </div>

        {inspectionError ? <div className="error-banner">{inspectionError}</div> : null}

        {document && inspection ? (
          <InspectionDocumentContent
            deferredChunkSearch={deferredChunkSearch}
            chunkSearch={chunkSearch}
            document={document}
            embeddingErrors={embeddingErrors}
            expandedEmbeddings={expandedEmbeddings}
            fullEmbeddings={fullEmbeddings}
            inspection={inspection}
            loadingEmbeddings={loadingEmbeddings}
            pageSize={pageSize}
            onChunkSearchChange={setChunkSearch}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            onToggleEmbedding={handleToggleEmbedding}
          />
        ) : (
          <div className="empty-state">
            <div>
              <h3>Documento nao carregado</h3>
              <p>O painel esta aguardando o retorno do backend para este documento.</p>
            </div>
          </div>
        )}
      </section>
    </main>
  );
}

function SummaryCard({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <article className="inspection-summary-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function InspectionDocumentContent({
  chunkSearch,
  deferredChunkSearch,
  document,
  embeddingErrors,
  expandedEmbeddings,
  fullEmbeddings,
  inspection,
  loadingEmbeddings,
  pageSize,
  onChunkSearchChange,
  onPageChange,
  onPageSizeChange,
  onToggleEmbedding
}: Readonly<{
  chunkSearch: string;
  deferredChunkSearch: string;
  document: DocumentInspection['document'];
  embeddingErrors: Record<string, string>;
  expandedEmbeddings: Record<string, boolean>;
  fullEmbeddings: Record<string, DocumentChunkEmbedding>;
  inspection: DocumentInspection;
  loadingEmbeddings: Record<string, boolean>;
  pageSize: number;
  onChunkSearchChange: (value: string) => void;
  onPageChange: React.Dispatch<React.SetStateAction<number>>;
  onPageSizeChange: React.Dispatch<React.SetStateAction<number>>;
  onToggleEmbedding: (chunk: DocumentChunkInspection) => Promise<void>;
}>) {
  return (
    <>
      <header className="inspection-document-header">
        <div>
          <h3>{document.title}</h3>
          <p>
            {document.metadata.category ?? 'Sem categoria'} • {document.contentType}
            {document.source ? ` • ${document.source}` : ''}
          </p>
          <p>
            <a href={getDocumentContentUrl(document.documentId)} rel="noreferrer" target="_blank">
              Ver arquivo original: {document.originalFileName || document.title}
            </a>
          </p>
        </div>
        <div className="status-row">
          <span className={clsx('badge', getStatusColor(document.status))}>{getStatusLabel(document.status)}</span>
          <span className="badge badge-neutral">v{document.version}</span>
        </div>
      </header>

      <div className="inspection-summary-grid">
        <SummaryCard label="Chunks persistidos" value={String(inspection.totalChunkCount)} />
        <SummaryCard label="Chunks filtrados" value={String(inspection.filteredChunkCount)} />
        <SummaryCard label="Embeddings presentes" value={String(inspection.embeddedChunkCount)} />
        <SummaryCard label="Ultima atualizacao" value={formatDateTime(document.updatedAtUtc ?? document.createdAtUtc)} />
        <SummaryCard label="Tags" value={document.metadata.tags.length > 0 ? document.metadata.tags.join(', ') : 'Sem tags'} />
      </div>

      <div className="chunk-toolbar">
        <label>
          <span>Buscar dentro dos chunks</span>
          <input
            value={chunkSearch}
            onChange={(event) => onChunkSearchChange(event.target.value)}
            placeholder="Texto, secao, chunk id ou metadata"
          />
        </label>
        <label>
          <span>Chunks por pagina</span>
          <select value={pageSize} onChange={(event) => onPageSizeChange(Number(event.target.value))}>
            {PAGE_SIZE_OPTIONS.map((value) => (
              <option key={value} value={value}>{value}</option>
            ))}
          </select>
        </label>
      </div>

      {inspection.chunks.length > 0 ? (
        <>
          <div className="pagination-bar">
            <span className="field-hint">
              Mostrando {inspection.chunks.length === 0 ? 0 : (inspection.pageNumber - 1) * inspection.pageSize + 1}-{Math.min(inspection.pageNumber * inspection.pageSize, inspection.filteredChunkCount)} de {inspection.filteredChunkCount} chunk(s) filtrado(s), {inspection.totalChunkCount} no total.
            </span>
            <div className="button-row compact">
              <button className="button ghost" disabled={inspection.pageNumber === 1} onClick={() => onPageChange((current) => current - 1)} type="button">
                Pagina anterior
              </button>
              <span className="badge badge-neutral">Pagina {inspection.pageNumber} de {inspection.totalPages}</span>
              <button className="button ghost" disabled={inspection.pageNumber === inspection.totalPages} onClick={() => onPageChange((current) => current + 1)} type="button">
                Proxima pagina
              </button>
            </div>
          </div>

          <div className="chunk-list">
            {inspection.chunks.map((chunk) => (
              <ChunkCard
                key={chunk.chunkId}
                chunk={chunk}
                documentId={document.documentId}
                highlightQuery={deferredChunkSearch}
                fullEmbedding={fullEmbeddings[chunk.chunkId]}
                isEmbeddingExpanded={Boolean(expandedEmbeddings[chunk.chunkId])}
                embeddingError={embeddingErrors[chunk.chunkId] ?? ''}
                isEmbeddingLoading={Boolean(loadingEmbeddings[chunk.chunkId])}
                onToggleEmbedding={onToggleEmbedding}
              />
            ))}
          </div>
        </>
      ) : (
        <div className="empty-state compact">
          <h3>Sem chunks para este filtro</h3>
          <p>Altere a busca ou aguarde a indexacao terminar para visualizar os chunks deste documento.</p>
        </div>
      )}
    </>
  );
}

function ChunkCard({
  chunk,
  documentId,
  highlightQuery,
  fullEmbedding,
  isEmbeddingExpanded,
  embeddingError,
  isEmbeddingLoading,
  onToggleEmbedding
}: Readonly<{
  chunk: DocumentChunkInspection;
  documentId: string;
  highlightQuery: string;
  fullEmbedding?: DocumentChunkEmbedding;
  isEmbeddingExpanded: boolean;
  embeddingError: string;
  isEmbeddingLoading: boolean;
  onToggleEmbedding: (chunk: DocumentChunkInspection) => Promise<void>;
}>) {
  const metadataEntries = Object.entries(chunk.metadata).filter(([key]) => key !== 'chunkIndex');
  const documentContentUrl = getDocumentContentUrl(documentId, chunk.pageNumber);
  let toggleButtonLabel = 'Mostrar vetor completo';
  if (isEmbeddingLoading) {
    toggleButtonLabel = 'Carregando vetor...';
  } else if (isEmbeddingExpanded) {
    toggleButtonLabel = 'Ocultar vetor completo';
  }

  return (
    <article className="chunk-card">
      <header className="chunk-card-header">
        <div>
          <strong>Chunk {formatChunkIndex(chunk.chunkIndex)}</strong>
          <p>
            {formatPageLabel(chunk)}
            {chunk.section ? ` • ${chunk.section}` : ''}
          </p>
        </div>
        <div className="status-row">
          <span className="badge badge-neutral">{chunk.characterCount} caracteres</span>
          <span className={clsx('badge', chunk.embedding.exists ? 'badge-success' : 'badge-warning')}>
            {chunk.embedding.exists ? 'Embedding ok' : 'Sem embedding'}
          </span>
        </div>
      </header>

      <pre className="chunk-content"><HighlightedChunkText text={chunk.content} query={highlightQuery} /></pre>

      <div className="embedding-preview-card">
        <strong>Embedding</strong>
        <p>
          {chunk.embedding.exists
            ? `${chunk.embedding.dimensions} dimensoes. Preview: ${formatEmbeddingPreview(chunk.embedding.preview)}`
            : 'Nenhum vetor persistido para este chunk.'}
        </p>
        {chunk.embedding.exists ? (
          <div className="button-row compact">
            <a className="button ghost" href={documentContentUrl} rel="noreferrer" target="_blank">
              Abrir chunk {formatChunkIndex(chunk.chunkIndex)} no documento
            </a>
            <button className="button ghost" disabled={isEmbeddingLoading} onClick={() => void onToggleEmbedding(chunk)} type="button">
              {toggleButtonLabel}
            </button>
          </div>
        ) : null}
        {embeddingError ? <div className="error-banner">{embeddingError}</div> : null}
        {isEmbeddingExpanded && fullEmbedding ? (
          <pre className="embedding-vector-panel">{formatFullEmbeddingVector(fullEmbedding.values)}</pre>
        ) : null}
      </div>

      {metadataEntries.length > 0 ? (
        <div className="chunk-metadata-grid">
          {metadataEntries.map(([key, value]) => (
            <div className="chunk-metadata-item" key={`${chunk.chunkId}-${key}`}>
              <span>{key}</span>
              <strong>{value}</strong>
            </div>
          ))}
        </div>
      ) : null}
    </article>
  );
}

function formatChunkIndex(chunkIndex: number) {
  return Number.isFinite(chunkIndex) && chunkIndex < 2147483647
    ? chunkIndex.toString().padStart(2, '0')
    : '--';
}

function formatPageLabel(chunk: DocumentChunkInspection) {
  if (!chunk.pageNumber) {
    return 'Pagina nao informada';
  }

  if (chunk.endPageNumber && chunk.endPageNumber !== chunk.pageNumber) {
    return `Paginas ${chunk.pageNumber}-${chunk.endPageNumber}`;
  }

  return `Pagina ${chunk.pageNumber}`;
}

function formatEmbeddingPreview(preview: number[]) {
  if (preview.length === 0) {
    return '[]';
  }

  return `[${preview.map((value) => value.toFixed(4)).join(', ')}]`;
}

function formatFullEmbeddingVector(values: number[]) {
  return values.map((value, index) => `${index + 1}. ${value.toFixed(6)}`).join('\n');
}

function HighlightedChunkText({ text, query }: Readonly<{ text: string; query: string }>) {
  const normalizedQuery = query.trim();
  if (!normalizedQuery) {
    return <>{text}</>;
  }

  const expression = new RegExp(`(${escapeRegex(normalizedQuery)})`, 'gi');
  const parts = text.split(expression);

  return parts.map((part, index) => (
    part.toLocaleLowerCase() === normalizedQuery.toLocaleLowerCase()
      ? <mark className="chunk-highlight" key={`${part}-${index}`}>{part}</mark>
      : <React.Fragment key={`${part}-${index}`}>{part}</React.Fragment>
  ));
}

function escapeRegex(value: string) {
  return value.replaceAll(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`);
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString('pt-BR');
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