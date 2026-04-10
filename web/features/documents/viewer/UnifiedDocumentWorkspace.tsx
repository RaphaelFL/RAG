'use client';

import * as React from 'react';
import clsx from 'clsx';
import { getDocumentContentBlob, getDocumentContentUrl } from '@/features/documents/api/documentsApi';
import type { DocumentChunkEmbedding, DocumentChunkInspection, DocumentInspection } from '@/features/documents/types/documents';
import { buildLocalStrategyChunks, buildViewerChunksFromInspection } from '@/features/documents/viewer/chunking';
import { DocumentNodeRenderer } from '@/features/documents/viewer/DocumentNodeRenderer';
import { exportChunkAsHtml, exportChunkAsJson, exportChunksAsHtmlArchive, exportChunksAsJson } from '@/features/documents/viewer/export';
import { parseDocumentBlob } from '@/features/documents/viewer/parsers';
import type { ChunkDisplayMode, ChunkingStrategyId, DocumentNode, UnifiedDocumentModel } from '@/features/documents/viewer/types';
import { buildViewerWarnings, buildOriginLabel, normalizeTextForMatching } from '@/features/documents/viewer/utils';

const CHUNKING_OPTIONS = {
  maxCharacters: 1400,
  overlapBlocks: 1
};

const STRATEGY_LABELS: Record<ChunkingStrategyId, string> = {
  'backend-synced': 'Chunks indexados atuais',
  paragraph: 'Por paragrafo',
  'semantic-block': 'Por bloco semantico',
  page: 'Por pagina/slide/aba',
  section: 'Por secao',
  heading: 'Por heading',
  'character-limit': 'Por limite com protecao estrutural'
};

export default function UnifiedDocumentWorkspace({
  activeChunkId = null,
  activeDisplayMode,
  document,
  inspection,
  fullEmbeddings,
  expandedEmbeddings,
  embeddingErrors,
  loadingEmbeddings,
  onToggleEmbedding
}: Readonly<{
  activeChunkId?: string | null;
  activeDisplayMode?: 'document' | 'chunk';
  document: DocumentInspection['document'];
  inspection: DocumentInspection;
  fullEmbeddings: Record<string, DocumentChunkEmbedding>;
  expandedEmbeddings: Record<string, boolean>;
  embeddingErrors: Record<string, string>;
  loadingEmbeddings: Record<string, boolean>;
  onToggleEmbedding: (chunk: DocumentChunkInspection) => Promise<void>;
}>) {
  const { useMemo, useState } = React;
  const [displayMode, setDisplayMode] = useState<'document' | 'chunk'>(activeDisplayMode ?? 'document');
  const [renderMode, setRenderMode] = useState<ChunkDisplayMode>('fidelity');
  const [strategyId, setStrategyId] = useState<ChunkingStrategyId>('backend-synced');
  const [selectedChunkId, setSelectedChunkId] = useState<string | null>(activeChunkId ?? inspection.chunks[0]?.chunkId ?? null);
  const [selectedOutlineId, setSelectedOutlineId] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);
  const viewportRef = React.useRef<HTMLDivElement | null>(null);
  const { isParsing, parseError, parsedDocument, parserWarnings } = useParsedDocument(document, setSelectedOutlineId);

  const viewerChunks = useMemo(
    () => buildWorkspaceChunks(parsedDocument, document, inspection.chunks, strategyId),
    [document, inspection.chunks, parsedDocument, strategyId]
  );

  const selectedChunk = useMemo(
    () => viewerChunks.find((chunk) => chunk.chunkId === selectedChunkId) ?? viewerChunks[0] ?? null,
    [selectedChunkId, viewerChunks]
  );

  React.useEffect(() => {
    if (!selectedChunk && viewerChunks[0]) {
      setSelectedChunkId(viewerChunks[0].chunkId);
      return;
    }

    if (selectedChunk && !viewerChunks.some((chunk) => chunk.chunkId === selectedChunk.chunkId)) {
      setSelectedChunkId(viewerChunks[0]?.chunkId ?? null);
    }
  }, [selectedChunk, viewerChunks]);

  React.useEffect(() => {
    if (activeChunkId) {
      setSelectedChunkId(activeChunkId);
    }
  }, [activeChunkId]);

  React.useEffect(() => {
    if (activeDisplayMode) {
      setDisplayMode(activeDisplayMode);
    }
  }, [activeDisplayMode]);

  React.useEffect(() => {
    if (!selectedOutlineId || !viewportRef.current) {
      return;
    }

    const target = viewportRef.current.querySelector<HTMLElement>(`[data-outline-target="${selectedOutlineId}"]`);
    if (target) {
      target.scrollIntoView({ block: 'center', behavior: 'smooth' });
    }
  }, [selectedOutlineId, displayMode, parsedDocument]);

  const warnings = useMemo(
    () => parsedDocument ? [...parserWarnings, ...buildViewerWarnings(parsedDocument, viewerChunks)] : parserWarnings,
    [parsedDocument, parserWarnings, viewerChunks]
  );
  const statusLabel = getViewerStatusLabel(isParsing, parsedDocument);

  return (
    <section className="document-workspace-shell">
      <WorkspaceToolbar
        displayMode={displayMode}
        outline={parsedDocument?.outline ?? []}
        renderMode={renderMode}
        selectedChunk={selectedChunk}
        selectedOutlineId={selectedOutlineId}
        strategyId={strategyId}
        zoom={zoom}
        onDisplayModeChange={setDisplayMode}
        onOutlineChange={setSelectedOutlineId}
        onRenderModeChange={setRenderMode}
        onStrategyChange={setStrategyId}
        onZoomChange={setZoom}
      />

      <WorkspaceActions
        document={document}
        parsedDocument={parsedDocument}
        selectedChunk={selectedChunk}
        viewerChunks={viewerChunks}
      />

      {warnings.length > 0 ? (
        <div className="viewer-warning-stack">
          {warnings.map((warning) => (
            <div className="info-banner" key={warning}>{warning}</div>
          ))}
        </div>
      ) : null}

      {parseError ? <div className="error-banner">{parseError}</div> : null}

      <div className="document-workspace-grid">
        <ChunkSidebar
          chunks={viewerChunks}
          expandedEmbeddings={expandedEmbeddings}
          embeddingErrors={embeddingErrors}
          fullEmbeddings={fullEmbeddings}
          inspectionChunks={inspection.chunks}
          loadingEmbeddings={loadingEmbeddings}
          selectedChunkId={selectedChunk?.chunkId ?? null}
          strategyId={strategyId}
          onDisplayChunk={(chunkId) => {
            setSelectedChunkId(chunkId);
            setDisplayMode('chunk');
          }}
          onDisplayDocumentOrigin={(chunkId) => {
            setSelectedChunkId(chunkId);
            setDisplayMode('document');
          }}
          onToggleEmbedding={onToggleEmbedding}
        />

        <ViewerPanel
          displayMode={displayMode}
          isParsing={isParsing}
          parsedDocument={parsedDocument}
          renderMode={renderMode}
          selectedChunk={selectedChunk}
          statusLabel={statusLabel}
          viewportRef={viewportRef}
          zoom={zoom}
        />
      </div>
    </section>
  );
}

function WorkspaceToolbar({
  displayMode,
  outline,
  renderMode,
  selectedChunk,
  selectedOutlineId,
  strategyId,
  zoom,
  onDisplayModeChange,
  onOutlineChange,
  onRenderModeChange,
  onStrategyChange,
  onZoomChange
}: Readonly<{
  displayMode: 'document' | 'chunk';
  outline: UnifiedDocumentModel['outline'];
  renderMode: ChunkDisplayMode;
  selectedChunk: ReturnType<typeof getSelectedChunk>;
  selectedOutlineId: string | null;
  strategyId: ChunkingStrategyId;
  zoom: number;
  onDisplayModeChange: (value: 'document' | 'chunk') => void;
  onOutlineChange: (value: string | null) => void;
  onRenderModeChange: (value: ChunkDisplayMode) => void;
  onStrategyChange: (value: ChunkingStrategyId) => void;
  onZoomChange: (value: number) => void;
}>) {
  return (
    <div className="document-workspace-toolbar">
      <div className="document-workspace-toggles">
        <div className="segmented-control">
          <button className={clsx('segment', displayMode === 'document' && 'is-active')} onClick={() => onDisplayModeChange('document')} type="button">
            Documento completo
          </button>
          <button className={clsx('segment', displayMode === 'chunk' && 'is-active')} disabled={!selectedChunk} onClick={() => onDisplayModeChange('chunk')} type="button">
            Chunk selecionado
          </button>
        </div>

        <div className="segmented-control">
          <button className={clsx('segment', renderMode === 'clean' && 'is-active')} onClick={() => onRenderModeChange('clean')} type="button">
            Leitura limpa
          </button>
          <button className={clsx('segment', renderMode === 'fidelity' && 'is-active')} onClick={() => onRenderModeChange('fidelity')} type="button">
            Fidelidade maxima
          </button>
        </div>
      </div>

      <div className="document-workspace-controls">
        <label>
          <span>Estrategia</span>
          <select value={strategyId} onChange={(event) => onStrategyChange(event.target.value as ChunkingStrategyId)}>
            {(Object.entries(STRATEGY_LABELS) as Array<[ChunkingStrategyId, string]>).map(([value, label]) => (
              <option key={value} value={value}>{label}</option>
            ))}
          </select>
        </label>

        <label>
          <span>Navegacao</span>
          <select value={selectedOutlineId ?? ''} onChange={(event) => onOutlineChange(event.target.value || null)}>
            <option value="">Documento inteiro</option>
            {outline.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>

        <label>
          <span>Zoom</span>
          <select value={String(zoom)} onChange={(event) => onZoomChange(Number(event.target.value))}>
            {[0.75, 0.9, 1, 1.1, 1.25, 1.5].map((value) => (
              <option key={value} value={value}>{Math.round(value * 100)}%</option>
            ))}
          </select>
        </label>
      </div>
    </div>
  );
}

function WorkspaceActions({
  document,
  parsedDocument,
  selectedChunk,
  viewerChunks
}: Readonly<{
  document: DocumentInspection['document'];
  parsedDocument: UnifiedDocumentModel | null;
  selectedChunk: ReturnType<typeof getSelectedChunk>;
  viewerChunks: ReturnType<typeof buildWorkspaceChunks>;
}>) {
  return (
    <div className="document-workspace-actions">
      <a className="button ghost" download={document.originalFileName} href={getDocumentContentUrl(document.documentId)}>
        Baixar documento original
      </a>
      {selectedChunk ? (
        <>
          <button className="button secondary" onClick={() => exportChunkAsHtml(selectedChunk)} type="button">
            Baixar chunk formatado
          </button>
          <button className="button ghost" onClick={() => exportChunkAsJson(selectedChunk)} type="button">
            Baixar chunk JSON
          </button>
        </>
      ) : null}
      {parsedDocument ? (
        <>
          <button className="button ghost" onClick={() => exportChunksAsJson(parsedDocument, viewerChunks)} type="button">
            Exportar chunks reutilizaveis
          </button>
          <button className="button ghost" onClick={() => exportChunksAsHtmlArchive(parsedDocument, viewerChunks)} type="button">
            Exportar chunks em HTML
          </button>
        </>
      ) : null}
    </div>
  );
}

function ChunkSidebar({
  chunks,
  expandedEmbeddings,
  embeddingErrors,
  fullEmbeddings,
  inspectionChunks,
  loadingEmbeddings,
  selectedChunkId,
  strategyId,
  onDisplayChunk,
  onDisplayDocumentOrigin,
  onToggleEmbedding
}: Readonly<{
  chunks: ReturnType<typeof buildWorkspaceChunks>;
  expandedEmbeddings: Record<string, boolean>;
  embeddingErrors: Record<string, string>;
  fullEmbeddings: Record<string, DocumentChunkEmbedding>;
  inspectionChunks: DocumentChunkInspection[];
  loadingEmbeddings: Record<string, boolean>;
  selectedChunkId: string | null;
  strategyId: ChunkingStrategyId;
  onDisplayChunk: (chunkId: string) => void;
  onDisplayDocumentOrigin: (chunkId: string) => void;
  onToggleEmbedding: (chunk: DocumentChunkInspection) => Promise<void>;
}>) {
  return (
    <aside className="document-chunk-sidebar panel">
      <div className="panel-header compact-header">
        <div>
          <h3>Chunks</h3>
          <p>{chunks.length} item(ns) para a estrategia atual.</p>
        </div>
        <span className="badge badge-neutral">{STRATEGY_LABELS[strategyId]}</span>
      </div>

      <div className="document-chunk-sidebar-list">
        {chunks.length === 0 ? (
          <div className="empty-state compact">
            <h3>Sem chunks</h3>
            <p>Nao foi possivel montar chunks estruturados para esta estrategia.</p>
          </div>
        ) : chunks.map((chunk) => (
          <ChunkSidebarCard
            key={chunk.chunkId}
            chunk={chunk}
            embeddingErrors={embeddingErrors}
            expandedEmbeddings={expandedEmbeddings}
            fullEmbeddings={fullEmbeddings}
            inspectionChunk={inspectionChunks.find((item) => item.chunkId === chunk.backendChunkId || item.chunkId === chunk.chunkId) ?? null}
            isSelected={selectedChunkId === chunk.chunkId}
            loadingEmbeddings={loadingEmbeddings}
            onDisplayChunk={onDisplayChunk}
            onDisplayDocumentOrigin={onDisplayDocumentOrigin}
            onToggleEmbedding={onToggleEmbedding}
          />
        ))}
      </div>
    </aside>
  );
}

function ChunkSidebarCard({
  chunk,
  embeddingErrors,
  expandedEmbeddings,
  fullEmbeddings,
  inspectionChunk,
  isSelected,
  loadingEmbeddings,
  onDisplayChunk,
  onDisplayDocumentOrigin,
  onToggleEmbedding
}: Readonly<{
  chunk: ReturnType<typeof buildWorkspaceChunks>[number];
  embeddingErrors: Record<string, string>;
  expandedEmbeddings: Record<string, boolean>;
  fullEmbeddings: Record<string, DocumentChunkEmbedding>;
  inspectionChunk: DocumentChunkInspection | null;
  isSelected: boolean;
  loadingEmbeddings: Record<string, boolean>;
  onDisplayChunk: (chunkId: string) => void;
  onDisplayDocumentOrigin: (chunkId: string) => void;
  onToggleEmbedding: (chunk: DocumentChunkInspection) => Promise<void>;
}>) {
  const embeddingToggleLabel = getEmbeddingToggleLabel(inspectionChunk, expandedEmbeddings, loadingEmbeddings);

  return (
    <article className={clsx('workspace-chunk-card', isSelected && 'is-selected')} key={chunk.chunkId}>
      <button className="workspace-chunk-hitbox" onClick={() => onDisplayChunk(chunk.chunkId)} type="button">
        <div className="workspace-chunk-card-header">
          <strong>{formatChunkLabel(chunk.chunkIndex)}</strong>
          <span className="badge badge-neutral">{chunk.originLabel}</span>
        </div>
        <p>{chunk.headingAncestry.at(-1) ?? 'Sem heading'}</p>
        <div className="workspace-chunk-meta">
          <span>{chunk.rawText.length} caracteres</span>
          <span>{chunk.sourceType}</span>
          {inspectionChunk?.embedding.exists ? <span>Embedding ok</span> : null}
        </div>
      </button>

      <div className="workspace-chunk-preview">
        <pre>{truncateText(chunk.rawText, 240)}</pre>
      </div>

      <div className="button-row compact workspace-chunk-actions">
        <button className="button secondary" onClick={() => onDisplayChunk(chunk.chunkId)} type="button">
          Ver trecho em tela
        </button>
        <button className="button ghost" onClick={() => onDisplayDocumentOrigin(chunk.chunkId)} type="button">
          Ver origem no documento
        </button>
        <button className="button ghost" onClick={() => exportChunkAsHtml(chunk)} type="button">
          Baixar chunk
        </button>
      </div>

      {inspectionChunk?.embedding.exists ? (
        <div className="workspace-embedding-card">
          <strong>Embedding</strong>
          <p>{inspectionChunk.embedding.dimensions} dimensoes</p>
          <div className="button-row compact">
            <button className="button ghost" disabled={Boolean(loadingEmbeddings[inspectionChunk.chunkId])} onClick={() => void onToggleEmbedding(inspectionChunk)} type="button">
              {embeddingToggleLabel}
            </button>
          </div>
          {embeddingErrors[inspectionChunk.chunkId] ? <div className="error-banner">{embeddingErrors[inspectionChunk.chunkId]}</div> : null}
          {expandedEmbeddings[inspectionChunk.chunkId] && fullEmbeddings[inspectionChunk.chunkId] ? (
            <pre className="embedding-vector-panel">{formatVector(fullEmbeddings[inspectionChunk.chunkId].values)}</pre>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

function ViewerPanel({
  displayMode,
  isParsing,
  parsedDocument,
  renderMode,
  selectedChunk,
  statusLabel,
  viewportRef,
  zoom
}: Readonly<{
  displayMode: 'document' | 'chunk';
  isParsing: boolean;
  parsedDocument: UnifiedDocumentModel | null;
  renderMode: ChunkDisplayMode;
  selectedChunk: ReturnType<typeof getSelectedChunk>;
  statusLabel: string;
  viewportRef: React.RefObject<HTMLDivElement | null>;
  zoom: number;
}>) {
  const content = displayMode === 'chunk'
    ? renderChunkStage(selectedChunk, renderMode, zoom)
    : renderDocumentStage(parsedDocument, selectedChunk, renderMode, viewportRef, zoom);

  return (
    <div className="document-viewer-panel panel">
      <div className="panel-header compact-header">
        <div>
          <h3>{displayMode === 'document' ? 'Documento original estruturado' : 'Chunk estruturado'}</h3>
          <p>
            {displayMode === 'document'
              ? 'O viewer destaca a origem do chunk selecionado dentro da representacao mais fiel disponivel.'
              : 'O chunk e renderizado com estrutura, estilos e hierarquia preservados sempre que o formato permitir.'}
          </p>
        </div>
        <span className={clsx('badge', isParsing ? 'badge-warning' : 'badge-success')}>
          {statusLabel}
        </span>
      </div>

      {isParsing ? <div className="info-banner">Carregando e estruturando o documento original...</div> : null}
      {content}
    </div>
  );
}

function renderChunkStage(selectedChunk: ReturnType<typeof getSelectedChunk>, renderMode: ChunkDisplayMode, zoom: number) {
  if (!selectedChunk) {
    return (
      <div className="empty-state compact">
        <h3>Nenhum chunk selecionado</h3>
        <p>Escolha um chunk no painel esquerdo para abrir a visualizacao fiel.</p>
      </div>
    );
  }

  return (
    <div className="document-viewer-stage">
      <div className="document-viewer-stage-meta">
        <div>
          <h4>{formatChunkStageTitle(selectedChunk.chunkIndex)}</h4>
          <p>{selectedChunk.originLabel}</p>
        </div>
        <div className="workspace-chunk-meta">
          <span>{selectedChunk.sourceType}</span>
          <span>{selectedChunk.contentHash}</span>
        </div>
      </div>
      <div className="document-zoom-shell" style={zoomStyle(zoom)}>
        <DocumentNodeRenderer
          className="chunk-render-surface"
          highlightRange={toHighlightRange(selectedChunk.sourceMap as { startOffset?: number; endOffset?: number })}
          mode={renderMode}
          nodes={selectedChunk.formattedContent.length > 0 ? selectedChunk.formattedContent : buildFallbackNodes(selectedChunk.rawText)}
        />
      </div>
    </div>
  );
}

function renderDocumentStage(
  parsedDocument: UnifiedDocumentModel | null,
  selectedChunk: ReturnType<typeof getSelectedChunk>,
  renderMode: ChunkDisplayMode,
  viewportRef: React.RefObject<HTMLDivElement | null>,
  zoom: number
) {
  if (!parsedDocument) {
    return (
      <div className="document-viewer-fallback">
        {selectedChunk ? <pre className="document-preview-content">{selectedChunk.rawText}</pre> : null}
        <p className="document-preview-note">
          Nao foi possivel materializar a visao estruturada deste formato no cliente. O documento original continua acessivel para download e abertura direta.
        </p>
      </div>
    );
  }

  return (
    <div className="document-viewer-stage" ref={viewportRef}>
      <div className="document-viewer-stage-meta">
        <div>
          <h4>{parsedDocument.metadata.title}</h4>
          <p>{parsedDocument.metadata.originalFileName}</p>
        </div>
        {selectedChunk ? (
          <div className="workspace-chunk-meta">
            <span>Origem destacada: {selectedChunk.originLabel}</span>
            <span>{selectedChunk.headingAncestry.join(' / ') || 'Sem heading'}</span>
          </div>
        ) : null}
      </div>
      <div className="document-zoom-shell" style={zoomStyle(zoom)}>
        <DocumentNodeRenderer
          className="document-render-surface"
          highlightRange={selectedChunk ? toHighlightRange(selectedChunk.sourceMap as { startOffset?: number; endOffset?: number }) : null}
          mode={renderMode}
          nodes={parsedDocument.root}
        />
      </div>
    </div>
  );
}

function useParsedDocument(
  document: DocumentInspection['document'],
  onDefaultOutline: React.Dispatch<React.SetStateAction<string | null>>
) {
  const [parsedDocument, setParsedDocument] = React.useState<UnifiedDocumentModel | null>(null);
  const [parserWarnings, setParserWarnings] = React.useState<string[]>([]);
  const [parseError, setParseError] = React.useState<string | null>(null);
  const [isParsing, setIsParsing] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    setIsParsing(true);
    setParseError(null);

    getDocumentContentBlob(document.documentId)
      .then(async (blob) => parseDocumentBlob({
        documentId: document.documentId,
        title: document.title,
        originalFileName: document.originalFileName,
        contentType: document.contentType,
        version: document.version,
        status: document.status,
        createdAtUtc: document.createdAtUtc,
        updatedAtUtc: document.updatedAtUtc,
        blob
      }))
      .then((payload) => {
        if (cancelled) {
          return;
        }

        setParsedDocument(payload.model);
        setParserWarnings(payload.parserWarnings);
        onDefaultOutline(payload.model.outline[0]?.id ?? null);
      })
      .catch((error) => {
        if (!cancelled) {
          setParsedDocument(null);
          setParserWarnings([]);
          setParseError(readableClientError(error));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsParsing(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [document]);

  return {
    isParsing,
    parseError,
    parsedDocument,
    parserWarnings
  };
}

function buildWorkspaceChunks(
  parsedDocument: UnifiedDocumentModel | null,
  document: DocumentInspection['document'],
  chunks: DocumentChunkInspection[],
  strategyId: ChunkingStrategyId
) {
  if (!parsedDocument) {
    return chunks.map((chunk) => buildFallbackViewerChunk(document, chunk));
  }

  return strategyId === 'backend-synced'
    ? buildViewerChunksFromInspection(parsedDocument, document.documentId, document.originalFileName, chunks)
    : buildLocalStrategyChunks(parsedDocument, document.documentId, document.originalFileName, strategyId, CHUNKING_OPTIONS);
}

function buildFallbackViewerChunk(document: DocumentInspection['document'], chunk: DocumentChunkInspection) {
  const sourceType = resolveSourceType(chunk.section, chunk.pageNumber);
  return {
    chunkId: chunk.chunkId,
    documentId: document.documentId,
    chunkIndex: chunk.chunkIndex,
    backendChunkId: chunk.chunkId,
    strategyId: 'backend-synced' as const,
    sourceType,
    rawText: chunk.content,
    normalizedText: normalizeTextForMatching(chunk.content),
    formattedContent: [],
    contentHash: chunk.chunkId,
    referenceFileName: document.originalFileName,
    parserVersion: 'fallback',
    originLabel: chunk.section ?? buildOriginLabel({ sourceType, pageNumber: chunk.pageNumber }),
    headingAncestry: [],
    sourceMap: {
      sourceType,
      pageNumber: chunk.pageNumber,
      endPageNumber: chunk.endPageNumber ?? undefined,
      sectionTitle: chunk.section ?? undefined
    },
    metadata: chunk.metadata,
    fragments: [],
    sourceNodeIds: []
  };
}

function resolveSourceType(section: string | null | undefined, pageNumber: number) {
  if (section) {
    return 'section' as const;
  }

  if (pageNumber) {
    return 'page' as const;
  }

  return 'range' as const;
}

function getSelectedChunk(chunks: ReturnType<typeof buildWorkspaceChunks>, selectedChunkId: string | null) {
  return chunks.find((chunk) => chunk.chunkId === selectedChunkId) ?? chunks[0] ?? null;
}

function getViewerStatusLabel(isParsing: boolean, parsedDocument: UnifiedDocumentModel | null) {
  if (isParsing) {
    return 'Processando';
  }

  return parsedDocument ? parsedDocument.metadata.format.toUpperCase() : 'Fallback';
}

function getEmbeddingToggleLabel(
  inspectionChunk: DocumentChunkInspection | null,
  expandedEmbeddings: Record<string, boolean>,
  loadingEmbeddings: Record<string, boolean>
) {
  if (!inspectionChunk) {
    return 'Mostrar vetor completo';
  }

  if (loadingEmbeddings[inspectionChunk.chunkId]) {
    return 'Carregando vetor...';
  }

  return expandedEmbeddings[inspectionChunk.chunkId] ? 'Ocultar vetor' : 'Mostrar vetor completo';
}

function buildFallbackNodes(text: string): DocumentNode[] {
  return [{
    id: 'fallback-paragraph',
    kind: 'paragraph' as const,
    children: [{
      id: 'fallback-text',
      kind: 'text' as const,
      text,
      style: {
        css: {
          whiteSpace: 'pre-wrap'
        },
        preserveWhitespace: true
      }
    }]
  }];
}

function zoomStyle(zoom: number) {
  return {
    '--document-zoom': String(zoom)
  } as React.CSSProperties;
}

function truncateText(value: string, limit: number) {
  return value.length > limit ? `${value.slice(0, limit)}...` : value;
}

function formatChunkLabel(chunkIndex: number) {
  return `Chunk ${String(chunkIndex).padStart(2, '0')}`;
}

function formatChunkStageTitle(chunkIndex: number) {
  return `Trecho do chunk ${String(chunkIndex).padStart(2, '0')}`;
}

function formatVector(values: number[]) {
  return values.map((value, index) => `${index + 1}. ${value.toFixed(6)}`).join('\n');
}

function readableClientError(error: unknown) {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'Erro inesperado ao preparar a visualizacao estruturada do documento.';
}

function toHighlightRange(sourceMap: { startOffset?: number; endOffset?: number }) {
  return {
    startOffset: sourceMap.startOffset,
    endOffset: sourceMap.endOffset
  };
}