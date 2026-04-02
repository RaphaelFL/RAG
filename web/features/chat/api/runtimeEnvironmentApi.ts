'use client';

import type { RuntimeEnvironment } from '@/types/app';

export async function saveRuntimeEnvironment(environment: RuntimeEnvironment) {
  const response = await fetch('/api/runtime/environment', {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(environment)
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => undefined);
    throw new Error(typeof payload?.message === 'string' ? payload.message : `HTTP ${response.status}`);
  }
}