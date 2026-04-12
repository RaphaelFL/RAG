'use client';

import * as React from 'react';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { saveRuntimeEnvironment } from '@/features/chat/api/runtimeEnvironmentApi';
import { getOperationalAuditFeed, getRagRuntimeSettings, updateRagRuntimeSettings } from '@/features/chat/api/runtimeAdminApi';
import type { OperationalAuditCategory, OperationalAuditEntry, RagRuntimeSettings } from '@/features/chat/types/chat';
import type { AuthMode, RuntimeEnvironment, UserRole } from '@/types/app';

const USER_ROLES: UserRole[] = ['TenantUser', 'Analyst', 'TenantAdmin', 'PlatformAdmin', 'McpClient'];
const AUTH_MODES: AuthMode[] = ['jwt', 'development-headers'];
const AUDIT_CATEGORIES: OperationalAuditCategory[] = ['retrieval', 'prompt-assembly', 'agent-run', 'tool-execution'];
const AUDIT_STATUSES = ['completed', 'failed', 'timeout', 'rejected', 'disabled'];

export default function AdminSettingsConsole() {
  const { environment, isReady, setEnvironment } = useRuntimeEnvironment();
  const [draftEnvironment, setDraftEnvironment] = React.useState(environment);
  const [accessError, setAccessError] = React.useState<string | null>(null);
  const [accessStatus, setAccessStatus] = React.useState<'idle' | 'saving' | 'saved'>('idle');
  const [runtimeSettings, setRuntimeSettings] = React.useState<RagRuntimeSettings | null>(null);
  const [runtimeDraft, setRuntimeDraft] = React.useState<RagRuntimeSettings | null>(null);
  const [runtimeError, setRuntimeError] = React.useState<string | null>(null);
  const [isRuntimeLoading, setIsRuntimeLoading] = React.useState(false);
  const [isRuntimeSaving, setIsRuntimeSaving] = React.useState(false);
  const [auditEntries, setAuditEntries] = React.useState<OperationalAuditEntry[]>([]);
  const [auditCursor, setAuditCursor] = React.useState<string | null>(null);
  const [auditError, setAuditError] = React.useState<string | null>(null);
  const [isAuditLoading, setIsAuditLoading] = React.useState(false);
  const [isAuditLoadingMore, setIsAuditLoadingMore] = React.useState(false);
  const [auditFilters, setAuditFilters] = React.useState({
    category: '',
    status: '',
    fromUtc: '',
    toUtc: '',
    limit: '20'
  });

  React.useEffect(() => {
    setDraftEnvironment(environment);
  }, [environment]);

  React.useEffect(() => {
    if (!isReady || !canAdministerRuntime(environment.userRole)) {
      return;
    }

    let cancelled = false;
    setIsRuntimeLoading(true);
    setRuntimeError(null);

    void getRagRuntimeSettings(environment)
      .then((settings) => {
        if (cancelled) {
          return;
        }

        setRuntimeSettings(settings);
        setRuntimeDraft(settings);
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setRuntimeError(readableClientError(error));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsRuntimeLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [environment, isReady]);

  React.useEffect(() => {
    if (!isReady || !canAdministerRuntime(environment.userRole)) {
      return;
    }

    void handleLoadAudit(true);
  }, [environment, isReady]);

  function updateEnvironmentField<K extends keyof RuntimeEnvironment>(key: K, value: RuntimeEnvironment[K]) {
    setDraftEnvironment((current) => ({ ...current, [key]: value }));
    setAccessStatus('idle');
  }

  function updateRuntimeField<K extends keyof RagRuntimeSettings>(key: K, value: RagRuntimeSettings[K]) {
    setRuntimeDraft((current) => (current ? { ...current, [key]: value } : current));
  }

  function updateAuditFilter<K extends keyof typeof auditFilters>(key: K, value: (typeof auditFilters)[K]) {
    setAuditFilters((current) => ({ ...current, [key]: value }));
  }

  async function handleSaveEnvironment() {
    setAccessStatus('saving');
    setAccessError(null);

    try {
      const savedEnvironment = await saveRuntimeEnvironment(draftEnvironment);
      setDraftEnvironment(savedEnvironment);
      setEnvironment(savedEnvironment);
      setAccessStatus('saved');
    } catch (error) {
      setAccessError(readableClientError(error));
      setAccessStatus('idle');
    }
  }

  async function handleSaveRuntimeSettings() {
    if (!runtimeDraft) {
      return;
    }

    setIsRuntimeSaving(true);
    setRuntimeError(null);

    try {
      const saved = await updateRagRuntimeSettings(environment, runtimeDraft);
      setRuntimeSettings(saved);
      setRuntimeDraft(saved);
    } catch (error) {
      setRuntimeError(readableClientError(error));
    } finally {
      setIsRuntimeSaving(false);
    }
  }

  async function handleLoadAudit(reset: boolean) {
    const loadingSetter = reset ? setIsAuditLoading : setIsAuditLoadingMore;
    loadingSetter(true);
    setAuditError(null);

    try {
      const result = await getOperationalAuditFeed(environment, {
        category: auditFilters.category || undefined,
        status: auditFilters.status || undefined,
        fromUtc: normalizeDateFilter(auditFilters.fromUtc),
        toUtc: normalizeDateFilter(auditFilters.toUtc),
        cursor: reset ? undefined : auditCursor || undefined,
        limit: Math.max(1, Math.min(100, Number(auditFilters.limit) || 20))
      });

      setAuditEntries((current) => (reset ? result.entries : current.concat(result.entries)));
      setAuditCursor(result.nextCursor ?? null);
    } catch (error) {
      setAuditError(readableClientError(error));
    } finally {
      loadingSetter(false);
    }
  }

  return (
    <main className="console-shell">
      <section className="console-hero">
        <p className="console-kicker">Controle administrativo</p>
        <h1>Configuracoes de administrador</h1>
        <p>
          As credenciais e cabecalhos sensiveis deixam de sair do navegador para o backend principal.
          A aplicacao web passa a encaminhar as chamadas internamente pelo servidor do Next.
        </p>
        <div className="hero-actions">
          <a className="button ghost" href="/">
            Voltar ao console
          </a>
          <a className="button secondary" href="/inspecao-documental">
            Inspecao documental
          </a>
        </div>
      </section>

      <section className="admin-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Ambiente</h2>
              <p>Edite o acesso ao backend e salve para atualizar a sessao protegida do frontend.</p>
            </div>
            <span className={clsx('badge', isReady ? 'badge-success' : 'badge-warning')}>
              {isReady ? 'Pronto' : 'Carregando'}
            </span>
          </div>

          {accessError ? <div className="error-banner">{accessError}</div> : null}
          {accessStatus === 'saved' ? <div className="info-banner">Acesso administrativo atualizado com sucesso.</div> : null}

          <div className="field-grid">
            <label>
              <span>API Base URL</span>
              <input
                value={draftEnvironment.apiBaseUrl}
                onChange={(event) => updateEnvironmentField('apiBaseUrl', event.target.value)}
              />
              <small className="field-hint">Use apenas a origem do backend, por exemplo http://localhost:15214.</small>
            </label>
            <label>
              <span>Bearer Token</span>
              <input
                value={draftEnvironment.token}
                onChange={(event) => updateEnvironmentField('token', event.target.value)}
                placeholder="Cole um bearer token valido"
              />
              <small className="field-hint">Gravado apenas em cookie httpOnly da sessao atual para o proxy interno.</small>
            </label>
            <label>
              <span>Modo de autenticacao</span>
              <select
                value={draftEnvironment.authMode}
                onChange={(event) => updateEnvironmentField('authMode', event.target.value as AuthMode)}
              >
                {AUTH_MODES.map((authMode) => (
                  <option key={authMode} value={authMode}>{authMode}</option>
                ))}
              </select>
            </label>
            <label>
              <span>Tenant Id</span>
              <input
                value={draftEnvironment.tenantId}
                onChange={(event) => updateEnvironmentField('tenantId', event.target.value)}
              />
            </label>
            <label>
              <span>User Id</span>
              <input
                value={draftEnvironment.userId}
                onChange={(event) => updateEnvironmentField('userId', event.target.value)}
              />
            </label>
            <label>
              <span>Role</span>
              <select
                value={draftEnvironment.userRole}
                onChange={(event) => updateEnvironmentField('userRole', event.target.value as UserRole)}
              >
                {USER_ROLES.map((role) => (
                  <option key={role} value={role}>{role}</option>
                ))}
              </select>
            </label>
          </div>

          <div className="button-row">
            <button className="button secondary" disabled={accessStatus === 'saving'} onClick={() => void handleSaveEnvironment()} type="button">
              {accessStatus === 'saving' ? 'Salvando acesso...' : 'Salvar acesso'}
            </button>
          </div>
        </div>

        <RuntimeTuningPanel
          canAdminister={canAdministerRuntime(environment.userRole)}
          isLoading={isRuntimeLoading}
          isSaving={isRuntimeSaving}
          error={runtimeError}
          currentSettings={runtimeSettings}
          draftSettings={runtimeDraft}
          onNumberChange={(key, value) => updateRuntimeField(key, value)}
          onSave={handleSaveRuntimeSettings}
        />

        <OperationalAuditPanel
          canAdminister={canAdministerRuntime(environment.userRole)}
          entries={auditEntries}
          error={auditError}
          filters={auditFilters}
          hasNextPage={Boolean(auditCursor)}
          isLoading={isAuditLoading}
          isLoadingMore={isAuditLoadingMore}
          onFilterChange={updateAuditFilter}
          onRefresh={() => void handleLoadAudit(true)}
          onLoadMore={() => void handleLoadAudit(false)}
        />
      </section>
    </main>
  );
}

function RuntimeTuningPanel({
  canAdminister,
  isLoading,
  isSaving,
  error,
  currentSettings,
  draftSettings,
  onNumberChange,
  onSave
}: Readonly<{
  canAdminister: boolean;
  isLoading: boolean;
  isSaving: boolean;
  error: string | null;
  currentSettings: RagRuntimeSettings | null;
  draftSettings: RagRuntimeSettings | null;
  onNumberChange: <K extends keyof RagRuntimeSettings>(key: K, value: RagRuntimeSettings[K]) => void;
  onSave: () => Promise<void>;
}>) {
  if (!canAdminister) {
    return (
      <div className="panel">
        <div className="panel-header">
          <div>
            <h2>Runtime RAG</h2>
            <p>Seu papel atual nao possui permissao para alterar parametros operacionais.</p>
          </div>
          <span className="badge badge-neutral">Somente leitura</span>
        </div>
      </div>
    );
  }

  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <h2>Runtime RAG</h2>
          <p>Ajustes operacionais em memoria para chunking, contexto e TTL. Os tamanhos de chunk funcionam como base e PDFs maiores podem usar uma janela mais granular automaticamente.</p>
        </div>
        {isLoading ? <span className="badge badge-warning">Carregando</span> : <span className="badge badge-success">Admin</span>}
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      {draftSettings === null ? (
        <p className="muted-copy">Sem configuracao carregada.</p>
      ) : (
        <>
          <div className="field-grid two-columns">
            <NumericField label="Dense chunk" value={draftSettings.denseChunkSize} onChange={(value) => onNumberChange('denseChunkSize', value)} />
            <NumericField label="Dense overlap" value={draftSettings.denseOverlap} onChange={(value) => onNumberChange('denseOverlap', value)} />
            <NumericField label="Narrative chunk" value={draftSettings.narrativeChunkSize} onChange={(value) => onNumberChange('narrativeChunkSize', value)} />
            <NumericField label="Narrative overlap" value={draftSettings.narrativeOverlap} onChange={(value) => onNumberChange('narrativeOverlap', value)} />
            <NumericField label="Min chunk chars" value={draftSettings.minimumChunkCharacters} onChange={(value) => onNumberChange('minimumChunkCharacters', value)} />
            <NumericField label="Max context chunks" value={draftSettings.maxContextChunks} onChange={(value) => onNumberChange('maxContextChunks', value)} />
            <NumericField label="Candidate multiplier" value={draftSettings.retrievalCandidateMultiplier} onChange={(value) => onNumberChange('retrievalCandidateMultiplier', value)} />
            <NumericField label="Max candidates" value={draftSettings.retrievalMaxCandidateCount} onChange={(value) => onNumberChange('retrievalMaxCandidateCount', value)} />
            <NumericField label="Retrieval TTL s" value={draftSettings.retrievalCacheTtlSeconds} onChange={(value) => onNumberChange('retrievalCacheTtlSeconds', value)} />
            <NumericField label="Chat TTL s" value={draftSettings.chatCompletionCacheTtlSeconds} onChange={(value) => onNumberChange('chatCompletionCacheTtlSeconds', value)} />
            <NumericField label="Embedding TTL h" value={draftSettings.embeddingCacheTtlHours} onChange={(value) => onNumberChange('embeddingCacheTtlHours', value)} />
            <DecimalField label="Min rerank" value={draftSettings.minimumRerankScore} onChange={(value) => onNumberChange('minimumRerankScore', value)} />
            <DecimalField label="Exact boost" value={draftSettings.exactMatchBoost} onChange={(value) => onNumberChange('exactMatchBoost', value)} />
            <DecimalField label="Title boost" value={draftSettings.titleMatchBoost} onChange={(value) => onNumberChange('titleMatchBoost', value)} />
            <DecimalField label="Filter boost" value={draftSettings.filterMatchBoost} onChange={(value) => onNumberChange('filterMatchBoost', value)} />
          </div>

          {currentSettings ? (
            <p className="muted-copy">Config atual ativa: contexto {currentSettings.maxContextChunks}, dense chunk base {currentSettings.denseChunkSize}, retrieval TTL {currentSettings.retrievalCacheTtlSeconds}s.</p>
          ) : null}

          <div className="button-row">
            <button className="button secondary" disabled={isSaving || isLoading} onClick={() => void onSave()} type="button">
              {isSaving ? 'Salvando...' : 'Salvar runtime'}
            </button>
          </div>
        </>
      )}
    </div>
  );
}

function NumericField({ label, value, onChange }: Readonly<{ label: string; value: number; onChange: (value: number) => void }>) {
  return (
    <label>
      <span>{label}</span>
      <input type="number" value={value} onChange={(event) => onChange(Number(event.target.value) || 0)} />
    </label>
  );
}

function DecimalField({ label, value, onChange }: Readonly<{ label: string; value: number; onChange: (value: number) => void }>) {
  return (
    <label>
      <span>{label}</span>
      <input type="number" step="0.01" value={value} onChange={(event) => onChange(Number(event.target.value) || 0)} />
    </label>
  );
}

function canAdministerRuntime(userRole: UserRole) {
  return userRole === 'Analyst' || userRole === 'TenantAdmin' || userRole === 'PlatformAdmin';
}

function OperationalAuditPanel({
  canAdminister,
  entries,
  error,
  filters,
  hasNextPage,
  isLoading,
  isLoadingMore,
  onFilterChange,
  onRefresh,
  onLoadMore
}: Readonly<{
  canAdminister: boolean;
  entries: OperationalAuditEntry[];
  error: string | null;
  filters: { category: string; status: string; fromUtc: string; toUtc: string; limit: string };
  hasNextPage: boolean;
  isLoading: boolean;
  isLoadingMore: boolean;
  onFilterChange: <K extends 'category' | 'status' | 'fromUtc' | 'toUtc' | 'limit'>(key: K, value: string) => void;
  onRefresh: () => void;
  onLoadMore: () => void;
}>) {
  if (!canAdminister) {
    return null;
  }

  return (
    <div className="panel panel-span-two">
      <div className="panel-header">
        <div>
          <h2>Auditoria operacional</h2>
          <p>Consulta retrieval, prompt assembly, agent runs e tool executions com filtros e cursor.</p>
        </div>
        <span className={clsx('badge', isLoading ? 'badge-warning' : 'badge-accent')}>
          {isLoading ? 'Atualizando' : 'Feed'}
        </span>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <div className="field-grid two-columns">
        <label>
          <span>Categoria</span>
          <select value={filters.category} onChange={(event) => onFilterChange('category', event.target.value)}>
            <option value="">Todas</option>
            {AUDIT_CATEGORIES.map((category) => (
              <option key={category} value={category}>{category}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Status</span>
          <select value={filters.status} onChange={(event) => onFilterChange('status', event.target.value)}>
            <option value="">Todos</option>
            {AUDIT_STATUSES.map((status) => (
              <option key={status} value={status}>{status}</option>
            ))}
          </select>
        </label>
        <label>
          <span>De</span>
          <input type="datetime-local" value={filters.fromUtc} onChange={(event) => onFilterChange('fromUtc', event.target.value)} />
        </label>
        <label>
          <span>Ate</span>
          <input type="datetime-local" value={filters.toUtc} onChange={(event) => onFilterChange('toUtc', event.target.value)} />
        </label>
        <label>
          <span>Limite</span>
          <input type="number" min={1} max={100} value={filters.limit} onChange={(event) => onFilterChange('limit', event.target.value)} />
        </label>
      </div>

      <div className="button-row compact">
        <button className="button secondary" disabled={isLoading} onClick={onRefresh} type="button">
          {isLoading ? 'Buscando...' : 'Aplicar filtros'}
        </button>
      </div>

      {entries.length === 0 && !isLoading ? <p className="muted-copy">Nenhum evento encontrado para os filtros atuais.</p> : null}

      <div className="audit-feed">
        {entries.map((entry) => (
          <article key={`${entry.category}-${entry.entryId}`} className="audit-entry">
            <div className="audit-entry-header">
              <div>
                <strong className="audit-entry-title">{entry.title}</strong>
                <p className="muted-copy">{entry.summary}</p>
              </div>
              <div className="status-row">
                <span className="badge badge-accent">{entry.category}</span>
                {entry.status ? <span className={clsx('badge', badgeClassForStatus(entry.status))}>{entry.status}</span> : null}
              </div>
            </div>
            <div className="audit-meta">
              <span>Criado: {formatTimestamp(entry.createdAtUtc)}</span>
              {entry.completedAtUtc ? <span>Concluido: {formatTimestamp(entry.completedAtUtc)}</span> : null}
            </div>
            {entry.detailsJson ? (
              <details>
                <summary>Detalhes</summary>
                <pre className="audit-json">{prettyPrintJson(entry.detailsJson)}</pre>
              </details>
            ) : null}
          </article>
        ))}
      </div>

      {hasNextPage ? (
        <div className="button-row compact">
          <button className="button ghost" disabled={isLoadingMore} onClick={onLoadMore} type="button">
            {isLoadingMore ? 'Carregando...' : 'Carregar mais'}
          </button>
        </div>
      ) : null}
    </div>
  );
}

function normalizeDateFilter(value: string) {
  if (!value.trim()) {
    return undefined;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

function prettyPrintJson(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function badgeClassForStatus(status: string) {
  if (status === 'completed') {
    return 'badge-success';
  }

  if (status === 'failed' || status === 'rejected') {
    return 'badge-danger';
  }

  return 'badge-warning';
}

function formatTimestamp(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('pt-BR');
}

function readableClientError(error: unknown) {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'Erro inesperado.';
}