'use client';

import * as React from 'react';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { getDocumentChunkEmbedding, getDocumentContentUrl, getDocumentInspectionPage, getDocumentTextPreview } from '@/features/documents/api/documentsApi';
import type { DocumentChunkEmbedding, DocumentChunkInspection, DocumentInspection, DocumentStatus, DocumentTextPreview } from '@/features/documents/types/documents';

const ACTIVE_DOCUMENT_STATUSES = new Set<DocumentStatus>(['Queued', 'Parsing', 'OcrProcessing', 'Chunking', 'Embedding', 'Indexing', 'ReindexPending']);
const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];
type ViewerMode = 'chunk' | 'document' | null;

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
  const [viewerMode, setViewerMode] = useState<ViewerMode>(null);
  const [selectedChunkPreview, setSelectedChunkPreview] = useState<DocumentChunkInspection | null>(null);
  const [documentPreview, setDocumentPreview] = useState<DocumentTextPreview | null>(null);
  const [documentPreviewError, setDocumentPreviewError] = useState<string | null>(null);
  const [isDocumentPreviewLoading, setIsDocumentPreviewLoading] = useState(false);
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

  React.useEffect(() => {
    setViewerMode(null);
    setSelectedChunkPreview(null);
    setDocumentPreview(null);
    setDocumentPreviewError(null);
  }, [documentId]);

  React.useEffect(() => {
    if (!inspection || !selectedChunkPreview) {
      return;
    }

    const refreshedChunk = inspection.chunks.find((chunk) => chunk.chunkId === selectedChunkPreview.chunkId);
    if (refreshedChunk) {
      setSelectedChunkPreview(refreshedChunk);
    }
  }, [inspection, selectedChunkPreview]);

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

  React.useEffect(() => {
    if (!isReady || !document || viewerMode !== 'document' || documentPreview || documentPreviewError) {
      return;
    }

    let cancelled = false;
    setIsDocumentPreviewLoading(true);

    getDocumentTextPreview(environment, document.documentId)
      .then((payload) => {
        if (!cancelled) {
          setDocumentPreview(payload);
        }
      })
      .catch((error) => {
        if (!cancelled) {
          setDocumentPreviewError(readableClientError(error));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsDocumentPreviewLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [document, documentPreview, documentPreviewError, environment, isReady, viewerMode]);

  function handleShowChunkPreview(chunk: DocumentChunkInspection) {
    setSelectedChunkPreview(chunk);
    setViewerMode('chunk');
  }

  function handleShowDocumentPreview() {
    setViewerMode('document');
    if (documentPreviewError) {
      setDocumentPreview(null);
      setDocumentPreviewError(null);
    }
  }

  function handleCloseViewer() {
    setViewerMode(null);
  }

  function handleDownloadChunk(chunk: DocumentChunkInspection) {
    downloadTextFile(buildChunkFileName(document?.title ?? documentId, chunk), buildChunkExportContent(document?.title ?? documentId, chunk));
  }

  function handleDownloadVisibleChunks() {
    if (!inspection) {
      return;
    }

    downloadTextFile(
      buildChunkListFileName(document?.title ?? documentId, inspection.pageNumber),
      buildVisibleChunksExport(document?.title ?? documentId, inspection.chunks)
    );
  }

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
            <>
              <button className="button secondary" onClick={handleShowDocumentPreview} type="button">
                Ver documento completo em tela
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
            documentPreview={documentPreview}
            documentPreviewError={documentPreviewError}
            isDocumentPreviewLoading={isDocumentPreviewLoading}
            loadingEmbeddings={loadingEmbeddings}
            pageSize={pageSize}
            selectedChunkPreview={selectedChunkPreview}
            viewerMode={viewerMode}
            onCloseViewer={handleCloseViewer}
            onChunkSearchChange={setChunkSearch}
            onDownloadChunk={handleDownloadChunk}
            onDownloadVisibleChunks={handleDownloadVisibleChunks}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            onShowChunkPreview={handleShowChunkPreview}
            onShowDocumentPreview={handleShowDocumentPreview}
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
  documentPreview,
  documentPreviewError,
  isDocumentPreviewLoading,
  loadingEmbeddings,
  pageSize,
  selectedChunkPreview,
  viewerMode,
  onCloseViewer,
  onChunkSearchChange,
  onDownloadChunk,
  onDownloadVisibleChunks,
  onPageChange,
  onPageSizeChange,
  onShowChunkPreview,
  onShowDocumentPreview,
  onToggleEmbedding
}: Readonly<{
  chunkSearch: string;
  deferredChunkSearch: string;
  document: DocumentInspection['document'];
  documentPreview: DocumentTextPreview | null;
  documentPreviewError: string | null;
  embeddingErrors: Record<string, string>;
  expandedEmbeddings: Record<string, boolean>;
  fullEmbeddings: Record<string, DocumentChunkEmbedding>;
  inspection: DocumentInspection;
  isDocumentPreviewLoading: boolean;
  loadingEmbeddings: Record<string, boolean>;
  pageSize: number;
  selectedChunkPreview: DocumentChunkInspection | null;
  viewerMode: ViewerMode;
  onCloseViewer: () => void;
  onChunkSearchChange: (value: string) => void;
  onDownloadChunk: (chunk: DocumentChunkInspection) => void;
  onDownloadVisibleChunks: () => void;
  onPageChange: React.Dispatch<React.SetStateAction<number>>;
  onPageSizeChange: React.Dispatch<React.SetStateAction<number>>;
  onShowChunkPreview: (chunk: DocumentChunkInspection) => void;
  onShowDocumentPreview: () => void;
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

      <DocumentVisualizationPanel
        document={document}
        documentPreview={documentPreview}
        documentPreviewError={documentPreviewError}
        isDocumentPreviewLoading={isDocumentPreviewLoading}
        selectedChunkPreview={selectedChunkPreview}
        viewerMode={viewerMode}
        onCloseViewer={onCloseViewer}
        onDownloadChunk={onDownloadChunk}
        onShowDocumentPreview={onShowDocumentPreview}
      />

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
          <button className="button secondary" onClick={onShowDocumentPreview} type="button">
            Ver documento completo em tela
          </button>
        </div>
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
                documentTitle={document.title}
                highlightQuery={deferredChunkSearch}
                fullEmbedding={fullEmbeddings[chunk.chunkId]}
                isEmbeddingExpanded={Boolean(expandedEmbeddings[chunk.chunkId])}
                embeddingError={embeddingErrors[chunk.chunkId] ?? ''}
                isEmbeddingLoading={Boolean(loadingEmbeddings[chunk.chunkId])}
                onDownloadChunk={onDownloadChunk}
                onShowChunkPreview={onShowChunkPreview}
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

function DocumentVisualizationPanel({
  document,
  documentPreview,
  documentPreviewError,
  isDocumentPreviewLoading,
  selectedChunkPreview,
  viewerMode,
  onCloseViewer,
  onDownloadChunk,
  onShowDocumentPreview
}: Readonly<{
  document: DocumentInspection['document'];
  documentPreview: DocumentTextPreview | null;
  documentPreviewError: string | null;
  isDocumentPreviewLoading: boolean;
  selectedChunkPreview: DocumentChunkInspection | null;
  viewerMode: ViewerMode;
  onCloseViewer: () => void;
  onDownloadChunk: (chunk: DocumentChunkInspection) => void;
  onShowDocumentPreview: () => void;
}>) {
  const documentContentUrl = getDocumentContentUrl(document.documentId);
  const title = viewerMode === 'chunk' && selectedChunkPreview
    ? `Trecho do chunk ${formatChunkIndex(selectedChunkPreview.chunkIndex)}`
    : 'Documento completo em tela';
  const description = viewerMode === 'chunk' && selectedChunkPreview
    ? 'Mostra apenas o trecho que originou o chunk selecionado, sem abrir o arquivo inteiro.'
    : 'Preview textual consolidado do documento indexado para leitura direta dentro da interface.';

  let panelContent: React.ReactNode;
  if (viewerMode === 'chunk' && selectedChunkPreview) {
    panelContent = (
      <ChunkVisualizationContent
        chunk={selectedChunkPreview}
        document={document}
        onDownloadChunk={onDownloadChunk}
        onShowDocumentPreview={onShowDocumentPreview}
      />
    );
  } else if (viewerMode === 'document') {
    panelContent = (
      <DocumentVisualizationContent
        documentPreview={documentPreview}
        documentPreviewError={documentPreviewError}
        isDocumentPreviewLoading={isDocumentPreviewLoading}
      />
    );
  } else {
    panelContent = <VisualizationEmptyState onShowDocumentPreview={onShowDocumentPreview} />;
  }

  return (
    <section className="document-visualization-panel">
      <div className="document-visualization-header">
        <div className="document-visualization-copy">
          <h3>{title}</h3>
          <p>{description}</p>
        </div>
        <div className="button-row compact preview-actions">
          <a className="button ghost" download={buildDocumentDownloadName(document)} href={documentContentUrl}>
            Baixar documento original
          </a>
          <a className="button ghost" href={documentContentUrl} rel="noreferrer" target="_blank">
            Abrir original
          </a>
          {viewerMode ? (
            <button className="button ghost" onClick={onCloseViewer} type="button">
              Fechar visualizacao
            </button>
          ) : null}
        </div>
      </div>
      {panelContent}
    </section>
  );
}

function ChunkVisualizationContent({
  chunk,
  document,
  onDownloadChunk,
  onShowDocumentPreview
}: Readonly<{
  chunk: DocumentChunkInspection;
  document: DocumentInspection['document'];
  onDownloadChunk: (chunk: DocumentChunkInspection) => void;
  onShowDocumentPreview: () => void;
}>) {
  return (
    <div className="document-preview-surface">
      <div className="document-preview-meta">
        <span className="badge badge-accent">{formatPageLabel(chunk)}</span>
        {chunk.section ? <span className="badge badge-neutral">{chunk.section}</span> : null}
        <span className="badge badge-neutral">{chunk.characterCount} caracteres</span>
      </div>
      <pre className="document-preview-content">{chunk.content}</pre>
      <div className="button-row compact preview-actions">
        <button className="button secondary" onClick={() => onDownloadChunk(chunk)} type="button">
          Baixar chunk
        </button>
        <button className="button ghost" onClick={onShowDocumentPreview} type="button">
          Ver documento completo em tela
        </button>
        <a className="button ghost" href={getDocumentContentUrl(document.documentId, chunk.pageNumber)} rel="noreferrer" target="_blank">
          Abrir pagina original
        </a>
      </div>
    </div>
  );
}

function DocumentVisualizationContent({
  documentPreview,
  documentPreviewError,
  isDocumentPreviewLoading
}: Readonly<{
  documentPreview: DocumentTextPreview | null;
  documentPreviewError: string | null;
  isDocumentPreviewLoading: boolean;
}>) {
  return (
    <div className="document-preview-surface">
      <div className="document-preview-meta">
        <span className="badge badge-accent">{documentPreview?.chunkCount ?? 0} chunks consolidados</span>
        <span className="badge badge-neutral">{documentPreview?.characterCount ?? 0} caracteres</span>
      </div>
      {documentPreviewError ? <div className="error-banner">{documentPreviewError}</div> : null}
      {isDocumentPreviewLoading ? <div className="info-banner">Carregando preview textual do documento...</div> : null}
      {documentPreview ? <pre className="document-preview-content">{documentPreview.content}</pre> : null}
      {!isDocumentPreviewLoading && !documentPreviewError && !documentPreview ? (
        <div className="empty-state compact">
          <h3>Preview indisponivel</h3>
          <p>O documento ainda nao gerou um preview textual consolidado.</p>
        </div>
      ) : null}
      <p className="document-preview-note">
        Esta visualizacao prioriza leitura e responsividade. O arquivo original continua disponivel separadamente para abertura e download.
      </p>
    </div>
  );
}

function VisualizationEmptyState({ onShowDocumentPreview }: Readonly<{ onShowDocumentPreview: () => void }>) {
  return (
    <div className="empty-state compact">
      <h3>Selecione uma visualizacao</h3>
      <p>Abra um chunk para inspecionar apenas o trecho correspondente ou carregue o documento completo em tela.</p>
      <div className="button-row compact preview-actions">
        <button className="button secondary" onClick={onShowDocumentPreview} type="button">
          Ver documento completo em tela
        </button>
      </div>
    </div>
  );
}

function ChunkCard({
  chunk,
  documentId,
  documentTitle,
  highlightQuery,
  fullEmbedding,
  isEmbeddingExpanded,
  embeddingError,
  isEmbeddingLoading,
  onDownloadChunk,
  onShowChunkPreview,
  onToggleEmbedding
}: Readonly<{
  chunk: DocumentChunkInspection;
  documentId: string;
  documentTitle: string;
  highlightQuery: string;
  fullEmbedding?: DocumentChunkEmbedding;
  isEmbeddingExpanded: boolean;
  embeddingError: string;
  isEmbeddingLoading: boolean;
  onDownloadChunk: (chunk: DocumentChunkInspection) => void;
  onShowChunkPreview: (chunk: DocumentChunkInspection) => void;
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

      <div className="button-row chunk-action-row">
        <button className="button secondary" onClick={() => onShowChunkPreview(chunk)} type="button">
          Ver trecho em tela
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