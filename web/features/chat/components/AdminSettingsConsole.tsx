'use client';

import * as React from 'react';
import clsx from 'clsx';
import { useRuntimeEnvironment } from '@/features/chat/state/useRuntimeEnvironment';
import { saveRuntimeEnvironment } from '@/features/chat/api/runtimeEnvironmentApi';
import { getRagRuntimeSettings, updateRagRuntimeSettings } from '@/features/chat/api/runtimeAdminApi';
import type { RagRuntimeSettings } from '@/features/chat/types/chat';
import type { AuthMode, RuntimeEnvironment, UserRole } from '@/types/app';

const USER_ROLES: UserRole[] = ['TenantUser', 'Analyst', 'TenantAdmin', 'PlatformAdmin', 'McpClient'];
const AUTH_MODES: AuthMode[] = ['jwt', 'development-headers'];

export function AdminSettingsConsole() {
  const { environment, isReady, setEnvironment } = useRuntimeEnvironment();
  const [draftEnvironment, setDraftEnvironment] = React.useState(environment);
  const [accessError, setAccessError] = React.useState<string | null>(null);
  const [accessStatus, setAccessStatus] = React.useState<'idle' | 'saving' | 'saved'>('idle');
  const [runtimeSettings, setRuntimeSettings] = React.useState<RagRuntimeSettings | null>(null);
  const [runtimeDraft, setRuntimeDraft] = React.useState<RagRuntimeSettings | null>(null);
  const [runtimeError, setRuntimeError] = React.useState<string | null>(null);
  const [isRuntimeLoading, setIsRuntimeLoading] = React.useState(false);
  const [isRuntimeSaving, setIsRuntimeSaving] = React.useState(false);

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

  function updateEnvironmentField<K extends keyof RuntimeEnvironment>(key: K, value: RuntimeEnvironment[K]) {
    setDraftEnvironment((current) => ({ ...current, [key]: value }));
    setAccessStatus('idle');
  }

  function updateRuntimeField<K extends keyof RagRuntimeSettings>(key: K, value: RagRuntimeSettings[K]) {
    setRuntimeDraft((current) => (current ? { ...current, [key]: value } : current));
  }

  async function handleSaveEnvironment() {
    setAccessStatus('saving');
    setAccessError(null);

    try {
      await saveRuntimeEnvironment(draftEnvironment);
      setEnvironment(draftEnvironment);
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
          <p>Ajustes operacionais em memoria para chunking, contexto e TTL.</p>
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
            <p className="muted-copy">Config atual ativa: contexto {currentSettings.maxContextChunks}, dense chunk {currentSettings.denseChunkSize}, retrieval TTL {currentSettings.retrievalCacheTtlSeconds}s.</p>
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

function readableClientError(error: unknown) {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'Erro inesperado.';
}