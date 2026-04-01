'use client';

import { useEffect, useState } from 'react';
import { createDefaultEnvironment } from '@/lib/publicEnv';
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
        setEnvironment({ ...DEFAULT_ENVIRONMENT, ...parsed, token: DEFAULT_ENVIRONMENT.token });
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

    const persisted: PersistedEnvironment = {
      apiBaseUrl: environment.apiBaseUrl,
      tenantId: environment.tenantId,
      userId: environment.userId,
      userRole: environment.userRole
    };

    globalThis.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(persisted));
  }, [environment, isReady]);

  return {
    environment,
    isReady,
    setEnvironment
  };
}
