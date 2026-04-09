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
    const saved = globalThis.sessionStorage.getItem(STORAGE_KEY);
    if (saved) {
      try {
        const parsed = JSON.parse(saved) as PersistedEnvironment;
        const sanitized = normalizeRuntimeEnvironment({ ...DEFAULT_ENVIRONMENT, ...parsed, token: DEFAULT_ENVIRONMENT.token });
        setEnvironment(sanitized);
      } catch {
        globalThis.sessionStorage.removeItem(STORAGE_KEY);
      }
    }

    setIsReady(true);
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

  useEffect(() => {
    if (!isReady) {
      return;
    }

    saveRuntimeEnvironment(environment).catch(() => undefined);
  }, [environment, isReady]);

  return {
    environment,
    isReady,
    setEnvironment
  };
}
