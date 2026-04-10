import { afterEach, describe, expect, it, vi } from 'vitest';

describe('createDefaultEnvironment', () => {
  afterEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
  });

  it('usa development-headers por padrao quando a API publica aponta para localhost em production', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://localhost:15214');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_BEARER_TOKEN', 'local-dev-token');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_USER_ID', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_USER_ROLE', 'TenantAdmin');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_TEMPLATE_ID', 'grounded_answer');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION', '1.0.0');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_USE_STREAMING', 'true');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE', 'false');
    vi.stubEnv(
      'NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS',
      'http://localhost:15214,https://localhost:15213,http://localhost:3001'
    );

    const { createDefaultEnvironment } = await import('@/lib/publicEnv');

    expect(createDefaultEnvironment().authMode).toBe('development-headers');
  });

  it('mantem jwt por padrao quando a API publica nao e loopback em production', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'https://api.example.com');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_BEARER_TOKEN', 'prod-token');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_USER_ID', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_USER_ROLE', 'TenantAdmin');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_TEMPLATE_ID', 'grounded_answer');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION', '1.0.0');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_USE_STREAMING', 'true');
    vi.stubEnv('NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE', 'false');
    vi.stubEnv('NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS', 'https://api.example.com,https://app.example.com');

    const { createDefaultEnvironment } = await import('@/lib/publicEnv');

    expect(createDefaultEnvironment().authMode).toBe('jwt');
  });
});