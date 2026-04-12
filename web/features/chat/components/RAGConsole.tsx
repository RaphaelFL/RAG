'use client';

import * as React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { useChat } from '@/features/chat/hooks/useChat';
import { useDocumentUploads } from '@/features/documents/hooks/useDocumentUploads';
import { publicRuntimeDefaults } from '@/lib/publicEnv';
import type { ChatMessageModel, Citation } from '@/features/chat/types/chat';
import type { DocumentMetadataSuggestion } from '@/features/documents/types/documents';
import type { UserRole } from '@/types/app';

export function RAGConsole() {
  const { useDeferredValue, useState } = React;
  const { environment } = useRuntimeEnvironment();
  const [activeSessionId, setActiveSessionId] = useState('');
  const [message, setMessage] = useState('');
  const [templateId] = useState(publicRuntimeDefaults.templateId);
  const [templateVersion] = useState(publicRuntimeDefaults.templateVersion);
  const useStreaming = publicRuntimeDefaults.useStreaming;

  const chat = useChat({ environment, sessionId: activeSessionId });
  const documents = useDocumentUploads(environment, activeSessionId);
  const permissions = getRolePermissions(environment.userRole);
  const indexedConversationDocuments = React.useMemo(
    () => documents.uploads.filter((entry) => entry.documentId && entry.status === 'Indexed'),
    [documents.uploads]
  );
  const conversationDocumentIds = React.useMemo(
    () => indexedConversationDocuments.map((entry) => entry.documentId as string),
    [indexedConversationDocuments]
  );
  const pendingConversationDocuments = React.useMemo(
    () => documents.uploads.filter((entry) => !entry.documentId || entry.status !== 'Indexed'),
    [documents.uploads]
  );

  const deferredAssistantCount = useDeferredValue(chat.assistantMessages.length);
  let submitLabel = 'Enviar pergunta';
  if (chat.isLoading) {
    submitLabel = 'Enviando...';
  } else if (chat.isStreaming) {
    submitLabel = 'Transmitindo...';
  }

  React.useEffect(() => {
    if (!activeSessionId) {
      setActiveSessionId(createSessionId());
    }
  }, [activeSessionId]);

  async function submitMessage() {
    if (!message.trim()) {
      return;
    }

    const resolvedSessionId = activeSessionId || crypto.randomUUID();
    if (resolvedSessionId !== activeSessionId) {
      setActiveSessionId(resolvedSessionId);
    }

    const currentMessage = message;
    setMessage('');

    await chat.sendMessage({
      sessionId: resolvedSessionId,
      message: currentMessage,
      templateId,
      templateVersion,
      documentIds: [],
      useStreaming
    });
  }

  function handleSubmit(event: { preventDefault: () => void }) {
    event.preventDefault();
    void submitMessage();
  }

  function handleNewSession() {
    setActiveSessionId(createSessionId());
    chat.resetConversation();
  }

  return (
    <main className="console-shell">
      <section className="console-hero">
        <p className="console-kicker">Next.js 15 + React 19</p>
        <h1>RAG Console</h1>
        <p>
          Frontend do chatbot corporativo com streaming SSE, upload documental, citations auditaveis
          e historico de sessao isolado por tenant.
        </p>
        <div className="hero-actions">
          <button className="button ghost" onClick={handleNewSession} type="button">
            Nova sessao
          </button>
          <a className="button secondary" href="/inspecao-documental">
            Inspecao documental
          </a>
          <a className="button secondary" href="/configuracoes-de-administrador">
            Configuracoes de administrador
          </a>
        </div>
        <p className="field-hint">A sessao da conversa permanece interna e nao e exibida na interface principal.</p>
      </section>

      <section className="console-main console-main-standalone">
          <div className="panel panel-chat">
            <div className="panel-header panel-header-tight">
              <div>
                <h2>Conversa</h2>
                <p>
                  {deferredAssistantCount > 0
                    ? `${deferredAssistantCount} resposta(s) recebida(s)`
                    : 'Nenhuma resposta ainda'}
                </p>
              </div>
              <div className="status-row">
                <span className={clsx('badge', chat.isStreaming ? 'badge-accent' : 'badge-neutral')}>
                  {chat.isStreaming ? 'Streaming ativo' : 'Streaming ocioso'}
                </span>
                {chat.lastUsage ? (
                  <span className="badge badge-neutral">{chat.lastUsage.latencyMs} ms</span>
                ) : null}
              </div>
            </div>

            {chat.error ? <div className="error-banner">{chat.error}</div> : null}

            <UploadPanel
              uploads={documents.uploads}
              error={documents.error}
              bulkReindex={documents.lastBulkReindex}
              onSuggestMetadata={documents.suggestUploadMetadata}
              onUpload={documents.submitUpload}
              onReindex={documents.triggerReindex}
              onTenantFullReindex={documents.triggerTenantFullReindex}
              canUpload={permissions.canUpload}
              canIncrementalReindex={permissions.canIncrementalReindex}
              canFullReindex={permissions.canFullReindex}
              userRole={environment.userRole}
            />

            <div className="messages-panel">
              {chat.messages.length === 0 ? (
                <div className="empty-state">
                  <h3>Sem mensagens</h3>
                  <p>Envie uma pergunta grounded ou indexe documentos para esta conversa atual.</p>
                </div>
              ) : (
                chat.messages.map((entry) => <MessageCard key={entry.id} message={entry} />)
              )}
            </div>

            <form className="composer" onSubmit={handleSubmit}>

              <p className="field-hint">
                {buildConversationGroundingHint({
                  indexedDocumentCount: conversationDocumentIds.length,
                  pendingDocumentCount: pendingConversationDocuments.length
                })}
              </p>

              <label>
                <span>Pergunta</span>
                <textarea
                  rows={5}
                  value={message}
                  onChange={(event) => setMessage(event.target.value)}
                  placeholder="Pergunte algo sobre a base documental privada..."
                />
              </label>

              <div className="button-row">
                <button className="button primary" disabled={chat.isLoading || chat.isStreaming || !message.trim()} type="submit">
                  {submitLabel}
                </button>
                <button className="button secondary" disabled={!chat.isStreaming} onClick={chat.cancelStreaming} type="button">
                  Cancelar stream
                </button>
              </div>
            </form>
          </div>
      </section>
    </main>
  );
}

function createSessionId() {
  return crypto.randomUUID();
}

export function MessageCard({ message }: Readonly<{ message: ChatMessageModel }>) {
  return (
    <article className={clsx('message-card', message.role === 'assistant' ? 'assistant' : 'user')}>
      <header className="message-header">
        <div>
          <strong>{message.role === 'assistant' ? 'Assistant' : 'User'}</strong>
          <span>{new Date(message.createdAtUtc).toLocaleString('pt-BR')}</span>
        </div>
        {message.isStreaming ? <span className="badge badge-accent">Streaming</span> : null}
      </header>

      <div className="message-markdown">
        <ReactMarkdown rehypePlugins={[rehypeSanitize]} remarkPlugins={[remarkGfm]}>
          {message.content || (message.isStreaming ? '_Gerando resposta..._' : '_Sem conteudo._')}
        </ReactMarkdown>
      </div>

      {message.citations.length > 0 ? (
        <div className="citations-grid">
          {message.citations.map((citation) => (
            <CitationCard key={`${citation.documentId}-${citation.chunkId}`} citation={citation} />
          ))}
        </div>
      ) : null}

      {message.usage ? (
        <footer className="usage-strip">
          <span>{message.usage.model}</span>
          <span>{message.usage.totalTokens} tokens</span>
          <span>{message.usage.latencyMs} ms</span>
          <span>{message.usage.retrievalStrategy}</span>
          {message.usage.runtimeMetrics ? Object.entries(message.usage.runtimeMetrics).map(([key, value]) => (
            <span key={key}>{key}: {value}</span>
          )) : null}
        </footer>
      ) : null}
    </article>
  );
}


function CitationCard({ citation }: Readonly<{ citation: Citation }>) {
  const pageLabel = formatCitationPageRange(citation);
  const chunkLabel = formatCitationChunkLabel(citation.chunkId);
  const snippet = sanitizeCitationText(citation.snippet);
  const sectionLabel = sanitizeCitationText(citation.location?.section ?? '');

  return (
    <article className="citation-card">
      <header>
        <strong>{citation.documentTitle}</strong>
        <span>{chunkLabel}</span>
      </header>
      <p>{snippet}</p>
      <footer>
        <span>score {citation.score.toFixed(2)}</span>
        {pageLabel ? <span>{pageLabel}</span> : null}
        {sectionLabel ? <span>secao {sectionLabel}</span> : null}
      </footer>
    </article>
  );
}

function UploadPanel({
  uploads,
  error,
  bulkReindex,
  onSuggestMetadata,
  onUpload,
  onReindex,
  onTenantFullReindex,
  canUpload,
  canIncrementalReindex,
  canFullReindex,
  userRole
}: Readonly<{
  uploads: Array<{
    localId: string;
    conversationSessionId: string;
    fileName: string;
    documentId?: string;
    ingestionJobId?: string;
    status: string;
    logicalTitle?: string;
    category?: string;
    tags?: string[];
    error?: string;
    statusMessage?: string;
    details?: {
      title: string;
      version: number;
      lastJobId?: string | null;
    };
  }>;
  error: string | null;
  bulkReindex: ReturnType<typeof useDocumentUploads>['lastBulkReindex'];
  onSuggestMetadata: (file: File) => Promise<DocumentMetadataSuggestion>;
  onUpload: (input: {
    file: File;
    title?: string;
    category?: string;
    categories?: string[];
    tags?: string[];
    source?: string;
  }) => Promise<void>;
  onReindex: (documentId: string, fullReindex: boolean) => Promise<void>;
  onTenantFullReindex: () => Promise<void>;
  canUpload: boolean;
  canIncrementalReindex: boolean;
  canFullReindex: boolean;
  userRole: UserRole;
}>) {
  const uploadDraft = useUploadDraft(onSuggestMetadata, onUpload);

  function handleIncrementalReindex(documentId: string) {
    return onReindex(documentId, false);
  }

  function handleFullReindex(documentId: string) {
    return onReindex(documentId, true);
  }

  return (
    <div className="panel panel-embedded conversation-document-panel">
      <div className="panel-header">
        <div>
          <h2>Documentos da conversa</h2>
          <p>Analise o arquivo, revise os campos sugeridos e confirme o envio dentro desta sessao.</p>
        </div>
      </div>

      <div className="info-banner">
        Papel atual: <strong>{userRole}</strong>. Upload {canUpload ? 'liberado' : 'bloqueado'}; reindex incremental {canIncrementalReindex ? 'liberado' : 'bloqueado'}; reindex full {canFullReindex ? 'liberado' : 'bloqueado'}.
      </div>

      {canFullReindex ? (
        <div className="button-row compact">
          <button className="button secondary" onClick={() => void onTenantFullReindex()} type="button">
            Reindex full do tenant
          </button>
          {bulkReindex ? <span className="field-hint">Job {bulkReindex.jobId} enfileirado para {bulkReindex.documentCount} documento(s).</span> : null}
        </div>
      ) : null}

      {error ? <div className="error-banner">{error}</div> : null}
      {uploadDraft.analysisError ? <div className="error-banner">{uploadDraft.analysisError}</div> : null}

      <UploadDraftForm canUpload={canUpload} uploadDraft={uploadDraft} />

      <UploadHistoryList
        uploads={uploads}
        onFullReindex={handleFullReindex}
        onIncrementalReindex={handleIncrementalReindex}
        canIncrementalReindex={canIncrementalReindex}
        canFullReindex={canFullReindex}
      />
    </div>
  );
}

function UploadDraftForm({
  canUpload,
  uploadDraft
}: Readonly<{
  canUpload: boolean;
  uploadDraft: ReturnType<typeof useUploadDraft>;
}>) {
  if (!canUpload) {
    return (
      <div className="empty-state compact">
        <h3>Upload indisponivel</h3>
        <p>Seu papel atual nao possui permissao padrao para iniciar ingestao documental.</p>
      </div>
    );
  }

  return (
    <form className="upload-form" onSubmit={uploadDraft.handleSubmit}>
      <label>
        <span>Arquivo</span>
        <input key={uploadDraft.fileInputKey} type="file" onChange={uploadDraft.handleFileChange} />
      </label>

      {uploadDraft.file ? <UploadAnalysisBanner uploadDraft={uploadDraft} /> : null}

      {uploadDraft.file ? (
        <div className="field-grid two-columns">
          <label>
            <span>Titulo logico</span>
            <input
              value={uploadDraft.title}
              onChange={(event) => uploadDraft.setTitle(event.target.value)}
              placeholder="Titulo sugerido pela analise"
            />
          </label>
          <label>
            <span>Categoria</span>
            <input
              value={uploadDraft.category}
              onChange={(event) => uploadDraft.setCategory(event.target.value)}
              placeholder="Categoria principal"
            />
          </label>
          <label className="field-span-2">
            <span>Tags</span>
            <input
              value={uploadDraft.tags}
              onChange={(event) => uploadDraft.setTags(event.target.value)}
              placeholder="tag1,tag2"
            />
          </label>
        </div>
      ) : null}

      <div className="button-row compact">
        <button className="button primary" disabled={!uploadDraft.file || uploadDraft.isAnalyzing} type="submit">
          Confirmar envio
        </button>
      </div>
    </form>
  );
}

function UploadAnalysisBanner({
  uploadDraft
}: Readonly<{
  uploadDraft: ReturnType<typeof useUploadDraft>;
}>) {
  const analysisHint = getUploadAnalysisHint(uploadDraft.isAnalyzing, uploadDraft.analysisStrategy);
  return (
    <div className="info-banner analysis-banner">
      <strong>{uploadDraft.isAnalyzing ? 'Analisando documento...' : 'Sugestao automatica pronta para revisao.'}</strong>
      <span className="field-hint">{analysisHint}</span>
      {uploadDraft.previewText ? <span className="analysis-preview">Trecho analisado: {uploadDraft.previewText}</span> : null}
    </div>
  );
}

function getUploadAnalysisHint(isAnalyzing: boolean, analysisStrategy: string) {
  if (isAnalyzing) {
    return 'Extraindo conteudo e sugerindo titulo logico, categoria e tags.';
  }

  if (analysisStrategy) {
    return `Estrategia: ${analysisStrategy}`;
  }

  return 'Revise a sugestao automatica e confirme o envio.';
}

function buildConversationGroundingHint({
  indexedDocumentCount,
  pendingDocumentCount
}: Readonly<{
  indexedDocumentCount: number;
  pendingDocumentCount: number;
}>) {
  if (indexedDocumentCount === 0 && pendingDocumentCount === 0) {
    return 'Nenhum upload recente nesta conversa. O backend consulta a base indexada do tenant para montar o contexto do RAG.';
  }

  const indexedLabel = `${indexedDocumentCount} documento(s) enviado(s) nesta conversa ja indexado(s)`;
  const pendingLabel = pendingDocumentCount > 0
    ? ` e ${pendingDocumentCount} ainda em processamento`
    : '';
  const generalLabel = '. Esses documentos passam a fazer parte da base consultada pelo RAG junto com os demais documentos do tenant.';

  return `${indexedLabel}${pendingLabel}${generalLabel}`;
}

function UploadActions({
  documentId,
  onIncrementalReindex,
  onFullReindex,
  canIncrementalReindex,
  canFullReindex
}: Readonly<{
  documentId: string;
  onIncrementalReindex: (documentId: string) => Promise<void>;
  onFullReindex: (documentId: string) => Promise<void>;
  canIncrementalReindex: boolean;
  canFullReindex: boolean;
}>) {
  if (!canIncrementalReindex && !canFullReindex) {
    return null;
  }

  return (
    <div className="button-row compact">
      {canIncrementalReindex ? (
        <button className="button ghost" onClick={() => onIncrementalReindex(documentId)} type="button">
          Reindex incremental
        </button>
      ) : null}
      {canFullReindex ? (
        <button className="button ghost" onClick={() => onFullReindex(documentId)} type="button">
          Reindex full
        </button>
      ) : null}
    </div>
  );
}

export function getRolePermissions(userRole: UserRole) {
  const canUpload = userRole === 'Analyst' || userRole === 'TenantAdmin' || userRole === 'PlatformAdmin';
  const canIncrementalReindex = canUpload;
  const canFullReindex = userRole === 'TenantAdmin' || userRole === 'PlatformAdmin';

  return {
    canUpload,
    canIncrementalReindex,
    canFullReindex
  };
}

function splitCsv(value: string) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

function readableClientError(error: unknown) {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'Erro inesperado.';
}

function useUploadDraft(
  onSuggestMetadata: (file: File) => Promise<DocumentMetadataSuggestion>,
  onUpload: (input: {
    file: File;
    title?: string;
    category?: string;
    categories?: string[];
    tags?: string[];
    source?: string;
  }) => Promise<void>
) {
  const [file, setFile] = React.useState<File | null>(null);
  const [fileInputKey, setFileInputKey] = React.useState(0);
  const [title, setTitle] = React.useState('');
  const [category, setCategory] = React.useState('');
  const [tags, setTags] = React.useState('');
  const [isAnalyzing, setIsAnalyzing] = React.useState(false);
  const [analysisError, setAnalysisError] = React.useState<string | null>(null);
  const [analysisStrategy, setAnalysisStrategy] = React.useState('');
  const [previewText, setPreviewText] = React.useState('');

  function resetDraft() {
    setFile(null);
    setFileInputKey((current) => current + 1);
    setTitle('');
    setCategory('');
    setTags('');
    setAnalysisError(null);
    setAnalysisStrategy('');
    setPreviewText('');
  }

  function applySuggestion(suggestion: DocumentMetadataSuggestion) {
    setTitle(suggestion.suggestedTitle);
    setCategory(suggestion.suggestedCategory ?? suggestion.suggestedCategories[0] ?? '');
    setTags(suggestion.suggestedTags.join(','));
    setAnalysisStrategy(suggestion.strategy);
    setPreviewText(suggestion.previewText);
  }

  async function handleFileChange(event: { target: { files: FileList | null } }) {
    const nextFile = event.target.files?.[0] ?? null;
    setFile(nextFile);
    setAnalysisError(null);
    setAnalysisStrategy('');
    setPreviewText('');

    if (!nextFile) {
      setTitle('');
      setCategory('');
      setTags('');
      return;
    }

    setIsAnalyzing(true);

    try {
      const suggestion = await onSuggestMetadata(nextFile);
      applySuggestion(suggestion);
    } catch (error) {
      setAnalysisError(readableClientError(error));
    } finally {
      setIsAnalyzing(false);
    }
  }

  async function submitUploadForm() {
    if (!file) {
      return;
    }

    await onUpload({
      file,
      title,
      category,
      categories: category ? [category] : [],
      tags: splitCsv(tags),
      source: 'frontend-console'
    });

    resetDraft();
  }

  function handleSubmit(event: { preventDefault: () => void }) {
    event.preventDefault();
    void submitUploadForm();
  }

  return {
    file,
    fileInputKey,
    title,
    setTitle,
    category,
    setCategory,
    tags,
    setTags,
    isAnalyzing,
    analysisError,
    analysisStrategy,
    previewText,
    handleFileChange,
    handleSubmit
  };
}

function formatCitationPageRange(citation: Citation) {
  const startPage = citation.location?.page;
  const endPage = citation.location?.endPage;

  if (!startPage) {
    return null;
  }

  if (endPage && endPage !== startPage) {
    return `paginas ${startPage}-${endPage}`;
  }

  return `pagina ${startPage}`;
}

function formatCitationChunkLabel(chunkId: string) {
  const normalizedChunkId = chunkId.trim();
  const trailingChunkMatch = normalizedChunkId.match(/(chunk[-_]\d+)$/i);
  if (trailingChunkMatch) {
    return trailingChunkMatch[1].replace(/_/g, '-').toLowerCase();
  }

  const sanitizedChunkId = sanitizeCitationText(normalizedChunkId);
  return sanitizedChunkId || normalizedChunkId;
}

function sanitizeCitationText(value: string) {
  return value
    .replace(/\b(?:[0-9a-f]{32,}|[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})\b/gi, ' ')
    .replace(/[-_:]+(?=\s|$)/g, '')
    .replace(/\s{2,}/g, ' ')
    .replace(/\s+([,.;:!?])/g, '$1')
    .trim();
}

function UploadHistoryList({
  uploads,
  onIncrementalReindex,
  onFullReindex,
  canIncrementalReindex,
  canFullReindex
}: Readonly<{
  uploads: Array<{
    localId: string;
    conversationSessionId: string;
    fileName: string;
    documentId?: string;
    ingestionJobId?: string;
    status: string;
    logicalTitle?: string;
    category?: string;
    tags?: string[];
    error?: string;
    statusMessage?: string;
    details?: {
      title: string;
      version: number;
      lastJobId?: string | null;
    };
  }>;
  onIncrementalReindex: (documentId: string) => Promise<void>;
  onFullReindex: (documentId: string) => Promise<void>;
  canIncrementalReindex: boolean;
  canFullReindex: boolean;
}>) {
  if (uploads.length === 0) {
    return (
      <div className="empty-state compact">
        <h3>Sem documentos</h3>
        <p>Nenhum documento foi enviado para esta sessao ainda.</p>
      </div>
    );
  }

  return (
    <div className="upload-history-list">
      {uploads.map((upload) => (
        <UploadStatusEntry
          key={upload.localId}
          upload={upload}
          onIncrementalReindex={onIncrementalReindex}
          onFullReindex={onFullReindex}
          canIncrementalReindex={canIncrementalReindex}
          canFullReindex={canFullReindex}
        />
      ))}
    </div>
  );
}

function UploadStatusEntry({
  upload,
  onIncrementalReindex,
  onFullReindex,
  canIncrementalReindex,
  canFullReindex
}: Readonly<{
  upload: {
    localId: string;
    conversationSessionId: string;
    fileName: string;
    documentId?: string;
    ingestionJobId?: string;
    status: string;
    logicalTitle?: string;
    category?: string;
    tags?: string[];
    error?: string;
    statusMessage?: string;
    details?: {
      title: string;
      version: number;
      lastJobId?: string | null;
    };
  };
  onIncrementalReindex: (documentId: string) => Promise<void>;
  onFullReindex: (documentId: string) => Promise<void>;
  canIncrementalReindex: boolean;
  canFullReindex: boolean;
}>) {
  const statusLabel = getStatusLabel(upload.status);
  const statusColor = getStatusColor(upload.status);
  const hasDetails = upload.details !== undefined;
  const hasActions = upload.documentId !== undefined;

  return (
    <article className="upload-status-entry">
      <header>
        <strong>{upload.logicalTitle ?? upload.fileName}</strong>
        <span className={clsx('badge', statusColor)}>{statusLabel}</span>
      </header>
      {upload.error ? <p className="error-details">{upload.error}</p> : null}
      {upload.statusMessage ? <p className="field-hint">{upload.statusMessage}</p> : null}
      <p className="field-hint">
        {upload.category ? `${upload.category} | ` : ''}
        {upload.tags?.join(', ')}
      </p>
      {hasDetails && upload.details ? (
        <p className="field-hint">
          {upload.details.title} v{upload.details.version}
        </p>
      ) : null}
      {hasActions ? (
        <UploadActions
          documentId={upload.documentId as string}
          onIncrementalReindex={onIncrementalReindex}
          onFullReindex={onFullReindex}
          canIncrementalReindex={canIncrementalReindex}
          canFullReindex={canFullReindex}
        />
      ) : null}
    </article>
  );
}

function getStatusColor(status: string) {
  switch (status) {
    case 'Requested':
    case 'Queued':
    case 'Processing':
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

function getStatusLabel(status: string) {
  switch (status) {
    case 'Requested':
      return 'Solicitado';
    case 'Queued':
      return 'Na fila';
    case 'Processing':
      return 'Processando';
    case 'Parsing':
      return 'Extraindo texto';
    case 'OcrProcessing':
      return 'Processando OCR';
    case 'Chunking':
      return 'Separando em chunks';
    case 'Embedding':
      return 'Gerando embeddings';
    case 'Indexing':
      return 'Indexando';
    case 'Indexed':
      return 'Indexado';
    case 'ReindexPending':
      return 'Reindexação pendente';
    case 'Failed':
      return 'Falhou';
    default:
      return status;
  }
}
