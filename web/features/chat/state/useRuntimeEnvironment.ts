'use client';

import { useEffect, useState } from 'react';
import { saveRuntimeEnvironment } from '@/features/chat/api/runtimeEnvironmentApi';
import { createDefaultEnvironment } from '@/lib/publicEnv';
import { normalizeRuntimeEnvironment } from '@/lib/runtimeEnvironment';
import type { RuntimeEnvironment } from '@/types/app';

const STORAGE_KEY = 'rag-console-environment';

type PersistedEnvironment = Omit<RuntimeEnvironment, 'token'>;

const DEFAULT_ENVIRONMENT = createDefaultEnvironment();

export function useRuntimeEnvironment() {
  const [environment, setEnvironment] = useState<RuntimeEnvironment>(DEFAULT_ENVIRONMENT);
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function initializeEnvironment() {
      let resolvedEnvironment = DEFAULT_ENVIRONMENT;
      const saved = globalThis.sessionStorage.getItem(STORAGE_KEY);

      if (saved) {
        try {
          const parsed = JSON.parse(saved) as PersistedEnvironment;
          resolvedEnvironment = normalizeRuntimeEnvironment({
            ...DEFAULT_ENVIRONMENT,
            ...parsed,
            token: DEFAULT_ENVIRONMENT.token
          });
        } catch {
          globalThis.sessionStorage.removeItem(STORAGE_KEY);
        }
      }

      if (!cancelled) {
        setEnvironment(resolvedEnvironment);
      }

      try {
        const savedEnvironment = await saveRuntimeEnvironment(resolvedEnvironment);
        if (!cancelled) {
          setEnvironment(savedEnvironment);
        }
      } catch {
        // O frontend continua funcional mesmo sem sincronizar o cookie na inicializacao.
      } finally {
        if (!cancelled) {
          setIsReady(true);
        }
      }
    }

    void initializeEnvironment();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!isReady) {
      return;
    }

    const sanitized = normalizeRuntimeEnvironment(environment);
    const persisted: PersistedEnvironment = {
      apiBaseUrl: sanitized.apiBaseUrl,
      authMode: sanitized.authMode,
      tenantId: sanitized.tenantId,
      userId: sanitized.userId,
      userRole: sanitized.userRole
    };

    globalThis.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(persisted));
  }, [environment, isReady]);

  return {
    environment,
    isReady,
    setEnvironment
  };
}
