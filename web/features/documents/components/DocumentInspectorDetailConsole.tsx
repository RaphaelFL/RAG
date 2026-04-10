'use client';

import * as React from 'react';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { getDocumentChunkEmbedding, getDocumentContentUrl, getDocumentInspectionPage } from '@/features/documents/api/documentsApi';
import type { DocumentChunkEmbedding, DocumentChunkInspection, DocumentInspection, DocumentStatus } from '@/features/documents/types/documents';
import UnifiedDocumentWorkspace from '@/features/documents/viewer/UnifiedDocumentWorkspace';

const ACTIVE_DOCUMENT_STATUSES = new Set<DocumentStatus>(['Queued', 'Parsing', 'OcrProcessing', 'Chunking', 'Embedding', 'Indexing', 'ReindexPending']);
const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

export default function DocumentInspectorDetailConsole({ documentId }: Readonly<{ documentId: string }>) {
  const { environment, isReady } = useRuntimeEnvironment();
  const [inspection, setInspection] = React.useState<DocumentInspection | null>(null);
  const [refreshKey, setRefreshKey] = React.useState(0);
  const [chunkSearch, setChunkSearch] = React.useState('');
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(10);
  const [inspectionError, setInspectionError] = React.useState<string | null>(null);
  const [isInspectionLoading, setIsInspectionLoading] = React.useState(false);
  const [expandedEmbeddings, setExpandedEmbeddings] = React.useState<Record<string, boolean>>({});
  const [fullEmbeddings, setFullEmbeddings] = React.useState<Record<string, DocumentChunkEmbedding>>({});
  const [embeddingErrors, setEmbeddingErrors] = React.useState<Record<string, string>>({});
  const [loadingEmbeddings, setLoadingEmbeddings] = React.useState<Record<string, boolean>>({});
  const [workspaceChunkId, setWorkspaceChunkId] = React.useState<string | null>(null);
  const [workspaceDisplayMode, setWorkspaceDisplayMode] = React.useState<'document' | 'chunk'>('document');
  const deferredChunkSearch = React.useDeferredValue(chunkSearch);

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

    void loadInspection();

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
  }, [deferredChunkSearch, documentId, pageSize]);

  React.useEffect(() => {
    setWorkspaceChunkId(null);
    setWorkspaceDisplayMode('document');
    setExpandedEmbeddings({});
    setFullEmbeddings({});
    setEmbeddingErrors({});
    setLoadingEmbeddings({});
  }, [documentId]);

  React.useEffect(() => {
    if (!inspection?.chunks.length) {
      setWorkspaceChunkId(null);
      return;
    }

    if (workspaceChunkId && inspection.chunks.some((chunk) => chunk.chunkId === workspaceChunkId)) {
      return;
    }

    setWorkspaceChunkId(inspection.chunks[0]?.chunkId ?? null);
  }, [inspection, workspaceChunkId]);

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

  function handleShowDocumentWorkspace() {
    setWorkspaceDisplayMode('document');
  }

  function handleShowChunkWorkspace(chunk: DocumentChunkInspection) {
    setWorkspaceChunkId(chunk.chunkId);
    setWorkspaceDisplayMode('chunk');
  }

  function handleDownloadVisibleChunks() {
    if (!inspection) {
      return;
    }

    downloadTextFile(
      buildChunkListFileName(inspection.document.title ?? documentId, inspection.pageNumber),
      buildVisibleChunksExport(inspection.document.title ?? documentId, inspection.chunks)
    );
  }

  const document = inspection?.document ?? null;

  return (
    <main className="console-shell">
      <section className="console-hero">
        <p className="console-kicker">Documento enviado</p>
        <h1>{document?.title ?? 'Inspecao documental'}</h1>
        <p>
          Visualize o documento original com estrutura preservada, confira os chunks indexados e abra o vetor completo apenas quando precisar investigar a indexacao.
        </p>
        <div className="hero-actions">
          <a className="button ghost" href="/inspecao-documental">
            Voltar para todos os documentos
          </a>
          {document ? (
            <>
              <button className="button secondary" onClick={handleShowDocumentWorkspace} type="button">
                Visualizar documento estruturado
              </button>
              <a className="button ghost" download={buildDocumentDownloadName(document)} href={getDocumentContentUrl(document.documentId)}>
                Baixar documento original
              </a>
              <a className="button ghost" href={getDocumentContentUrl(document.documentId)} rel="noreferrer" target="_blank">
                Abrir documento original
              </a>
            </>
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
                ? 'O viewer estruturado usa o arquivo original, e a lista abaixo continua exibindo exatamente os chunks persistidos pelo backend.'
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
            chunkSearch={chunkSearch}
            deferredChunkSearch={deferredChunkSearch}
            document={document}
            embeddingErrors={embeddingErrors}
            expandedEmbeddings={expandedEmbeddings}
            fullEmbeddings={fullEmbeddings}
            inspection={inspection}
            loadingEmbeddings={loadingEmbeddings}
            pageSize={pageSize}
            workspaceChunkId={workspaceChunkId}
            workspaceDisplayMode={workspaceDisplayMode}
            onChunkSearchChange={setChunkSearch}
            onDownloadVisibleChunks={handleDownloadVisibleChunks}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            onShowChunkWorkspace={handleShowChunkWorkspace}
            onShowDocumentWorkspace={handleShowDocumentWorkspace}
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
  workspaceChunkId,
  workspaceDisplayMode,
  onChunkSearchChange,
  onDownloadVisibleChunks,
  onPageChange,
  onPageSizeChange,
  onShowChunkWorkspace,
  onShowDocumentWorkspace,
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
  workspaceChunkId: string | null;
  workspaceDisplayMode: 'document' | 'chunk';
  onChunkSearchChange: (value: string) => void;
  onDownloadVisibleChunks: () => void;
  onPageChange: React.Dispatch<React.SetStateAction<number>>;
  onPageSizeChange: React.Dispatch<React.SetStateAction<number>>;
  onShowChunkWorkspace: (chunk: DocumentChunkInspection) => void;
  onShowDocumentWorkspace: () => void;
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

      <section className="document-visualization-panel workspace-panel">
        <div className="document-visualization-header">
          <div className="document-visualization-copy">
            <h3>Viewer estruturado</h3>
            <p>O arquivo original e reprocessado no navegador para preservar layout, headings, tabelas e a origem exata de cada chunk.</p>
          </div>
          <div className="button-row compact preview-actions">
            <button className="button secondary" onClick={onShowDocumentWorkspace} type="button">
              Documento completo
            </button>
          </div>
        </div>

        <UnifiedDocumentWorkspace
          activeChunkId={workspaceChunkId}
          activeDisplayMode={workspaceDisplayMode}
          document={document}
          embeddingErrors={embeddingErrors}
          expandedEmbeddings={expandedEmbeddings}
          fullEmbeddings={fullEmbeddings}
          inspection={inspection}
          loadingEmbeddings={loadingEmbeddings}
          onToggleEmbedding={onToggleEmbedding}
        />
      </section>

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
        <div className="button-row chunk-toolbar-actions">
          <button className="button ghost" onClick={onDownloadVisibleChunks} type="button">
            Baixar chunks visiveis
          </button>
        </div>
      </div>

      {inspection.chunks.length > 0 ? (
        <>
          <div className="pagination-bar">
            <span className="field-hint">
              Mostrando {inspection.chunks.length === 0 ? 0 : (inspection.pageNumber - 1) * inspection.pageSize + 1}-{Math.min(inspection.pageNumber * inspection.pageSize, inspection.filteredChunkCount)} de {inspection.filteredChunkCount} chunk(s) filtrado(s), {inspection.totalChunkCount} no total.
              {deferredChunkSearch ? ` Filtro ativo: ${deferredChunkSearch}.` : ''}
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
                documentTitle={document.title}
                embeddingError={embeddingErrors[chunk.chunkId] ?? ''}
                fullEmbedding={fullEmbeddings[chunk.chunkId]}
                highlightQuery={deferredChunkSearch}
                isEmbeddingExpanded={Boolean(expandedEmbeddings[chunk.chunkId])}
                isEmbeddingLoading={Boolean(loadingEmbeddings[chunk.chunkId])}
                isSelected={workspaceChunkId === chunk.chunkId && workspaceDisplayMode === 'chunk'}
                onDownloadChunk={(selectedChunk) => {
                  downloadTextFile(buildChunkFileName(document.title ?? document.documentId, selectedChunk), buildChunkExportContent(document.title ?? document.documentId, selectedChunk));
                }}
                onShowChunkWorkspace={onShowChunkWorkspace}
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
  documentTitle,
  embeddingError,
  fullEmbedding,
  highlightQuery,
  isEmbeddingExpanded,
  isEmbeddingLoading,
  isSelected,
  onDownloadChunk,
  onShowChunkWorkspace,
  onToggleEmbedding
}: Readonly<{
  chunk: DocumentChunkInspection;
  documentId: string;
  documentTitle: string;
  embeddingError: string;
  fullEmbedding?: DocumentChunkEmbedding;
  highlightQuery: string;
  isEmbeddingExpanded: boolean;
  isEmbeddingLoading: boolean;
  isSelected: boolean;
  onDownloadChunk: (chunk: DocumentChunkInspection) => void;
  onShowChunkWorkspace: (chunk: DocumentChunkInspection) => void;
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
    <article className={clsx('chunk-card', isSelected && 'chunk-card-selected')}>
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

      <div className="button-row chunk-action-row">
        <button className="button secondary" onClick={() => onShowChunkWorkspace(chunk)} type="button">
          Exibir no workspace
        </button>
        <button className="button ghost" onClick={() => onDownloadChunk(chunk)} type="button">
          Baixar chunk
        </button>
        <a className="button ghost" href={documentContentUrl} rel="noreferrer" target="_blank">
          Abrir pagina original
        </a>
      </div>

      <div className="embedding-preview-card">
        <strong>Embedding</strong>
        <p>
          {chunk.embedding.exists
            ? `${chunk.embedding.dimensions} dimensoes. Preview: ${formatEmbeddingPreview(chunk.embedding.preview)}`
            : 'Nenhum vetor persistido para este chunk.'}
        </p>
        {chunk.embedding.exists ? (
          <div className="button-row compact">
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

      <p className="field-hint">Origem do chunk: {documentTitle}</p>
    </article>
  );
}

function buildChunkExportContent(documentTitle: string, chunk: DocumentChunkInspection) {
  return [
    `Documento: ${documentTitle}`,
    `Chunk: ${formatChunkIndex(chunk.chunkIndex)}`,
    `Localizacao: ${formatPageLabel(chunk)}`,
    chunk.section ? `Secao: ${chunk.section}` : null,
    '',
    chunk.content
  ].filter((value): value is string => Boolean(value)).join('\n');
}

function buildVisibleChunksExport(documentTitle: string, chunks: DocumentChunkInspection[]) {
  return chunks.map((chunk) => buildChunkExportContent(documentTitle, chunk)).join('\n\n--------------------\n\n');
}

function buildChunkFileName(documentTitle: string, chunk: DocumentChunkInspection) {
  return sanitizeFileName(`${documentTitle}-chunk-${formatChunkIndex(chunk.chunkIndex)}.txt`);
}

function buildChunkListFileName(documentTitle: string, pageNumber: number) {
  return sanitizeFileName(`${documentTitle}-chunks-pagina-${pageNumber}.txt`);
}

function buildDocumentDownloadName(document: DocumentInspection['document']) {
  return sanitizeFileName(document.originalFileName || `${document.title}.bin`);
}

function sanitizeFileName(value: string) {
  return value.replaceAll(/[<>:"/\\|?*\u0000-\u001F]/g, '-');
}

function downloadTextFile(fileName: string, content: string) {
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const objectUrl = globalThis.URL.createObjectURL(blob);
  const link = globalThis.document.createElement('a');
  link.href = objectUrl;
  link.download = fileName;
  globalThis.document.body.append(link);
  link.click();
  link.remove();
  globalThis.setTimeout(() => globalThis.URL.revokeObjectURL(objectUrl), 0);
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