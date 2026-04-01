'use client';

import React from 'react';
import { useDeferredValue, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { useChat } from '@/features/chat/hooks/useChat';
import { useDocumentUploads } from '@/features/documents/hooks/useDocumentUploads';
import { publicRuntimeDefaults } from '@/lib/publicEnv';
import type { ChatMessageModel, Citation } from '@/features/chat/types/chat';
import type { UserRole } from '@/types/app';

const USER_ROLES: UserRole[] = ['TenantUser', 'Analyst', 'TenantAdmin', 'PlatformAdmin', 'McpClient'];

export function RAGConsole() {
  const { environment, isReady, setEnvironment } = useRuntimeEnvironment();
  const [sessionId, setSessionId] = useState(() => crypto.randomUUID());
  const [message, setMessage] = useState('');
  const [templateId, setTemplateId] = useState(publicRuntimeDefaults.templateId);
  const [templateVersion, setTemplateVersion] = useState(publicRuntimeDefaults.templateVersion);
  const [tagsInput, setTagsInput] = useState('');
  const [categoriesInput, setCategoriesInput] = useState('');
  const [useStreaming, setUseStreaming] = useState(publicRuntimeDefaults.useStreaming);
  const [allowGeneralKnowledge, setAllowGeneralKnowledge] = useState(publicRuntimeDefaults.allowGeneralKnowledge);

  const chat = useChat({ environment, sessionId });
  const documents = useDocumentUploads(environment);
  const permissions = getRolePermissions(environment.userRole);

  const deferredAssistantCount = useDeferredValue(chat.assistantMessages.length);
  let submitLabel = 'Enviar pergunta';
  if (chat.isLoading) {
    submitLabel = 'Enviando...';
  } else if (chat.isStreaming) {
    submitLabel = 'Transmitindo...';
  }

  async function submitMessage() {
    if (!message.trim()) {
      return;
    }

    const currentMessage = message;
    setMessage('');

    await chat.sendMessage({
      message: currentMessage,
      templateId,
      templateVersion,
      categories: splitCsv(categoriesInput),
      tags: splitCsv(tagsInput),
      useStreaming,
      allowGeneralKnowledge
    });
  }

  function handleSubmit(event: { preventDefault: () => void }) {
    event.preventDefault();
    void submitMessage();
  }

  async function handleLoadSession() {
    await chat.hydrateSession();
  }

  function handleNewSession() {
    setSessionId(crypto.randomUUID());
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
      </section>

      <section className="console-grid">
        <aside className="console-sidebar">
          <div className="panel">
            <div className="panel-header">
              <div>
                <h2>Ambiente</h2>
                <p>O frontend conversa apenas com a API HTTP do backend.</p>
              </div>
              <span className={clsx('badge', isReady ? 'badge-success' : 'badge-warning')}>
                {isReady ? 'Pronto' : 'Carregando'}
              </span>
            </div>

            <div className="field-grid">
              <label>
                <span>API Base URL</span>
                <input
                  value={environment.apiBaseUrl}
                  onChange={(event) => setEnvironment((current) => ({ ...current, apiBaseUrl: event.target.value }))}
                />
              </label>
              <label>
                <span>Bearer Token</span>
                <input
                  value={environment.token}
                  onChange={(event) => setEnvironment((current) => ({ ...current, token: event.target.value }))}
                  placeholder="Cole um bearer token valido"
                />
                <small className="field-hint">Nao persistido no navegador. Permanece apenas nesta sessao em memoria.</small>
              </label>
              <label>
                <span>Tenant Id</span>
                <input
                  value={environment.tenantId}
                  onChange={(event) => setEnvironment((current) => ({ ...current, tenantId: event.target.value }))}
                />
              </label>
              <label>
                <span>User Id</span>
                <input
                  value={environment.userId}
                  onChange={(event) => setEnvironment((current) => ({ ...current, userId: event.target.value }))}
                />
              </label>
              <label>
                <span>Role</span>
                <select
                  value={environment.userRole}
                  onChange={(event) => setEnvironment((current) => ({ ...current, userRole: event.target.value as UserRole }))}
                >
                  {USER_ROLES.map((role) => (
                    <option key={role} value={role}>{role}</option>
                  ))}
                </select>
              </label>
            </div>
          </div>

          <div className="panel">
            <div className="panel-header">
              <div>
                <h2>Sessao</h2>
                <p>Historico persistido pelo backend por tenant.</p>
              </div>
            </div>

            <label>
              <span>Session Id</span>
              <input value={sessionId} onChange={(event) => setSessionId(event.target.value)} />
            </label>

            <div className="button-row">
              <button className="button secondary" onClick={handleLoadSession} type="button">
                Carregar sessao
              </button>
              <button className="button ghost" onClick={handleNewSession} type="button">
                Nova sessao
              </button>
            </div>
          </div>

          <UploadPanel
            uploads={documents.uploads}
            error={documents.error}
            onUpload={documents.submitUpload}
            onReindex={documents.triggerReindex}
            canUpload={permissions.canUpload}
            canIncrementalReindex={permissions.canIncrementalReindex}
            canFullReindex={permissions.canFullReindex}
            userRole={environment.userRole}
          />
        </aside>

        <section className="console-main">
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

            <div className="messages-panel">
              {chat.messages.length === 0 ? (
                <div className="empty-state">
                  <h3>Sem mensagens</h3>
                  <p>Envie uma pergunta grounded, carregue uma sessao existente ou indexe documentos.</p>
                </div>
              ) : (
                chat.messages.map((entry) => <MessageCard key={entry.id} message={entry} />)
              )}
            </div>

            <form className="composer" onSubmit={handleSubmit}>
              <div className="field-grid two-columns">
                <label>
                  <span>Template</span>
                  <select value={templateId} onChange={(event) => setTemplateId(event.target.value)}>
                    <option value="grounded_answer">grounded_answer</option>
                    <option value="comparative_answer">comparative_answer</option>
                    <option value="document_summary">document_summary</option>
                  </select>
                </label>
                <label>
                  <span>Versao</span>
                  <input value={templateVersion} onChange={(event) => setTemplateVersion(event.target.value)} />
                </label>
                <label>
                  <span>Categorias</span>
                  <input value={categoriesInput} onChange={(event) => setCategoriesInput(event.target.value)} placeholder="financeiro,politicas" />
                </label>
                <label>
                  <span>Tags</span>
                  <input value={tagsInput} onChange={(event) => setTagsInput(event.target.value)} placeholder="reembolso,portal" />
                </label>
              </div>

              <div className="toggle-row">
                <label className="toggle">
                  <input type="checkbox" checked={useStreaming} onChange={(event) => setUseStreaming(event.target.checked)} />
                  <span>Streaming SSE</span>
                </label>
                <label className="toggle">
                  <input
                    type="checkbox"
                    checked={allowGeneralKnowledge}
                    onChange={(event) => setAllowGeneralKnowledge(event.target.checked)}
                  />
                  <span>Permitir conhecimento geral</span>
                </label>
              </div>

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
      </section>
    </main>
  );
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
          {message.content || '_Aguardando conteudo..._'}
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
        </footer>
      ) : null}
    </article>
  );
}

function CitationCard({ citation }: Readonly<{ citation: Citation }>) {
  return (
    <article className="citation-card">
      <header>
        <strong>{citation.documentTitle}</strong>
        <span>{citation.chunkId}</span>
      </header>
      <p>{citation.snippet}</p>
      <footer>
        <span>score {citation.score.toFixed(2)}</span>
        {citation.location?.page ? <span>p. {citation.location.page}</span> : null}
        {citation.location?.section ? <span>{citation.location.section}</span> : null}
      </footer>
    </article>
  );
}

function UploadPanel({
  uploads,
  error,
  onUpload,
  onReindex,
  canUpload,
  canIncrementalReindex,
  canFullReindex,
  userRole
}: Readonly<{
  uploads: Array<{
    localId: string;
    fileName: string;
    documentId?: string;
    ingestionJobId?: string;
    status: string;
    error?: string;
    details?: {
      title: string;
      version: number;
      lastJobId?: string | null;
    };
  }>;
  error: string | null;
  onUpload: (input: {
    file: File;
    title?: string;
    category?: string;
    categories?: string[];
    tags?: string[];
    source?: string;
  }) => Promise<void>;
  onReindex: (documentId: string, fullReindex: boolean) => Promise<void>;
  canUpload: boolean;
  canIncrementalReindex: boolean;
  canFullReindex: boolean;
  userRole: UserRole;
}>) {
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [category, setCategory] = useState('');
  const [tags, setTags] = useState('');

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

    setFile(null);
    setTitle('');
    setCategory('');
    setTags('');
  }

  function handleSubmit(event: { preventDefault: () => void }) {
    event.preventDefault();
    void submitUploadForm();
  }

  function handleIncrementalReindex(documentId: string) {
    return onReindex(documentId, false);
  }

  function handleFullReindex(documentId: string) {
    return onReindex(documentId, true);
  }

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Documentos</h2>
          <p>Upload, polling de status e reindexacao por papel.</p>
        </div>
      </div>

      <div className="info-banner">
        Papel atual: <strong>{userRole}</strong>. Upload {canUpload ? 'liberado' : 'bloqueado'}; reindex incremental {canIncrementalReindex ? 'liberado' : 'bloqueado'}; reindex full {canFullReindex ? 'liberado' : 'bloqueado'}.
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      {canUpload ? (
        <form className="upload-form" onSubmit={handleSubmit}>
          <label>
            <span>Arquivo</span>
            <input type="file" onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
          </label>
          <label>
            <span>Titulo logico</span>
            <input value={title} onChange={(event) => setTitle(event.target.value)} />
          </label>
          <label>
            <span>Categoria</span>
            <input value={category} onChange={(event) => setCategory(event.target.value)} placeholder="politicas" />
          </label>
          <label>
            <span>Tags</span>
            <input value={tags} onChange={(event) => setTags(event.target.value)} placeholder="financeiro,reembolso" />
          </label>
          <button className="button primary" disabled={!file} type="submit">
            Enviar documento
          </button>
        </form>
      ) : (
        <div className="empty-state compact">
          <h3>Upload indisponivel</h3>
          <p>Seu papel atual nao possui permissao padrao para iniciar ingestao documental.</p>
        </div>
      )}

      <div className="upload-list">
        {uploads.length === 0 ? (
          <p className="muted-copy">Nenhum documento enviado nesta sessao do navegador.</p>
        ) : (
          uploads.map((entry) => (
            <article key={entry.localId} className="upload-item">
              <div>
                <strong>{entry.details?.title ?? entry.fileName}</strong>
                <p>
                  status: {entry.status}
                  {entry.ingestionJobId ? ` · job ${entry.ingestionJobId}` : ''}
                  {entry.details?.version ? ` · v${entry.details.version}` : ''}
                </p>
                {entry.error ? <span className="upload-error">{entry.error}</span> : null}
              </div>
              {entry.documentId ? (
                <UploadActions
                  documentId={entry.documentId}
                  onFullReindex={handleFullReindex}
                  onIncrementalReindex={handleIncrementalReindex}
                  canIncrementalReindex={canIncrementalReindex}
                  canFullReindex={canFullReindex}
                />
              ) : null}
            </article>
          ))
        )}
      </div>
    </div>
  );
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
