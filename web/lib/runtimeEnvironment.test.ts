import { describe, expect, it } from 'vitest';
import { normalizeRuntimeEnvironment } from '@/lib/runtimeEnvironment';

describe('normalizeRuntimeEnvironment', () => {
  it('mantem apenas a origem quando apiBaseUrl vem com caminho de endpoint', () => {
    const environment = normalizeRuntimeEnvironment({
      apiBaseUrl: 'http://localhost:15214/api/v1/documents/suggest-metadata',
      authMode: 'development-headers',
      tenantId: '11111111-1111-1111-1111-111111111111',
      userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      userRole: 'TenantAdmin'
    });

    expect(environment.apiBaseUrl).toBe('http://localhost:15214');
  });

  it('volta para o padrao quando apiBaseUrl e invalida', () => {
    const environment = normalizeRuntimeEnvironment({
      apiBaseUrl: 'localhost:15214/api/v1/documents',
      authMode: 'development-headers',
      tenantId: '11111111-1111-1111-1111-111111111111',
      userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      userRole: 'TenantAdmin'
    });

    expect(environment.apiBaseUrl).toBe('http://localhost:15214');
  });
});