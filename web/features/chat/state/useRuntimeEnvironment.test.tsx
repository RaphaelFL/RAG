import { renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const saveRuntimeEnvironmentMock = vi.fn();

vi.mock('@/features/chat/api/runtimeEnvironmentApi', () => ({
  saveRuntimeEnvironment: (...args: unknown[]) => saveRuntimeEnvironmentMock(...args)
}));

describe('useRuntimeEnvironment', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.resetModules();
  });

  it('so fica pronto depois de sincronizar o ambiente inicial com o cookie', async () => {
    let resolveSave: ((value: unknown) => void) | null = null;
    saveRuntimeEnvironmentMock.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveSave = resolve;
        })
    );

    const { useRuntimeEnvironment } = await import('@/features/chat/state/useRuntimeEnvironment');
    const { result } = renderHook(() => useRuntimeEnvironment());

    expect(result.current.isReady).toBe(false);
    expect(saveRuntimeEnvironmentMock).toHaveBeenCalledTimes(1);

    resolveSave?.({
      apiBaseUrl: 'http://localhost:15214',
      token: 'local-dev-token',
      authMode: 'development-headers',
      tenantId: '11111111-1111-1111-1111-111111111111',
      userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      userRole: 'TenantAdmin'
    });

    await waitFor(() => {
      expect(result.current.isReady).toBe(true);
    });
  });

  it('usa valores persistidos da sessao antes de sincronizar o cookie', async () => {
    sessionStorage.setItem(
      'rag-console-environment',
      JSON.stringify({
        apiBaseUrl: 'http://127.0.0.1:15214',
        authMode: 'development-headers',
        tenantId: '11111111-1111-1111-1111-111111111111',
        userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        userRole: 'TenantAdmin'
      })
    );

    saveRuntimeEnvironmentMock.mockResolvedValue({
      apiBaseUrl: 'http://127.0.0.1:15214',
      token: 'local-dev-token',
      authMode: 'development-headers',
      tenantId: '11111111-1111-1111-1111-111111111111',
      userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      userRole: 'TenantAdmin'
    });

    const { useRuntimeEnvironment } = await import('@/features/chat/state/useRuntimeEnvironment');
    const { result } = renderHook(() => useRuntimeEnvironment());

    await waitFor(() => {
      expect(result.current.isReady).toBe(true);
    });

    expect(saveRuntimeEnvironmentMock).toHaveBeenCalledWith(
      expect.objectContaining({
        apiBaseUrl: 'http://127.0.0.1:15214',
        authMode: 'development-headers'
      })
    );
    expect(result.current.environment.apiBaseUrl).toBe('http://127.0.0.1:15214');
  });
});