import type { RuntimeEnvironment } from '@/types/app';

const developmentEnvFallback: Record<string, string> = {
  NEXT_PUBLIC_API_BASE_URL: 'http://localhost:15214',
  NEXT_PUBLIC_DEFAULT_BEARER_TOKEN: 'local-dev-token',
  NEXT_PUBLIC_AUTH_MODE: 'development-headers',
  NEXT_PUBLIC_DEFAULT_TENANT_ID: '11111111-1111-1111-1111-111111111111',
  NEXT_PUBLIC_DEFAULT_USER_ID: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  NEXT_PUBLIC_DEFAULT_USER_ROLE: 'TenantAdmin',
  NEXT_PUBLIC_DEFAULT_TEMPLATE_ID: 'grounded_answer',
  NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION: '1.0.0',
  NEXT_PUBLIC_DEFAULT_USE_STREAMING: 'true',
  NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE: 'false',
  NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS: 'http://localhost:15214,https://localhost:15213,http://localhost:5000'
};

const publicEnv = {
  NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL,
  NEXT_PUBLIC_DEFAULT_BEARER_TOKEN: process.env.NEXT_PUBLIC_DEFAULT_BEARER_TOKEN,
  NEXT_PUBLIC_AUTH_MODE: process.env.NEXT_PUBLIC_AUTH_MODE,
  NEXT_PUBLIC_DEFAULT_TENANT_ID: process.env.NEXT_PUBLIC_DEFAULT_TENANT_ID,
  NEXT_PUBLIC_DEFAULT_USER_ID: process.env.NEXT_PUBLIC_DEFAULT_USER_ID,
  NEXT_PUBLIC_DEFAULT_USER_ROLE: process.env.NEXT_PUBLIC_DEFAULT_USER_ROLE,
  NEXT_PUBLIC_DEFAULT_TEMPLATE_ID: process.env.NEXT_PUBLIC_DEFAULT_TEMPLATE_ID,
  NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION: process.env.NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION,
  NEXT_PUBLIC_DEFAULT_USE_STREAMING: process.env.NEXT_PUBLIC_DEFAULT_USE_STREAMING,
  NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE: process.env.NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE,
  NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS: process.env.NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS
} as const;

type PublicEnvKey = keyof typeof publicEnv;

export const publicRuntimeDefaults = {
  apiBaseUrl: requireEnv('NEXT_PUBLIC_API_BASE_URL'),
  token: readOptionalEnv('NEXT_PUBLIC_DEFAULT_BEARER_TOKEN'),
  authMode: resolveAuthMode(
    readOptionalEnv('NEXT_PUBLIC_AUTH_MODE') || inferDefaultAuthMode(requireEnv('NEXT_PUBLIC_API_BASE_URL'))
  ),
  tenantId: requireEnv('NEXT_PUBLIC_DEFAULT_TENANT_ID'),
  userId: requireEnv('NEXT_PUBLIC_DEFAULT_USER_ID'),
  userRole: resolveDefaultUserRole(requireEnv('NEXT_PUBLIC_DEFAULT_USER_ROLE')),
  templateId: requireEnv('NEXT_PUBLIC_DEFAULT_TEMPLATE_ID'),
  templateVersion: requireEnv('NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION'),
  useStreaming: resolveBoolean(requireEnv('NEXT_PUBLIC_DEFAULT_USE_STREAMING'), 'NEXT_PUBLIC_DEFAULT_USE_STREAMING'),
  allowGeneralKnowledge: resolveBoolean(requireEnv('NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE'), 'NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE'),
  connectSrcOrigins: resolveConnectSrcOrigins(
    requireEnv('NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS'),
    requireEnv('NEXT_PUBLIC_API_BASE_URL')
  )
} as const;

export function createDefaultEnvironment(): RuntimeEnvironment {
  return {
    apiBaseUrl: publicRuntimeDefaults.apiBaseUrl,
    token: publicRuntimeDefaults.token,
    authMode: publicRuntimeDefaults.authMode,
    tenantId: publicRuntimeDefaults.tenantId,
    userId: publicRuntimeDefaults.userId,
    userRole: publicRuntimeDefaults.userRole
  };
}

function resolveAuthMode(value: string | undefined): RuntimeEnvironment['authMode'] {
  switch (value) {
    case 'jwt':
    case 'development-headers':
      return value;
    default:
      throw new Error('NEXT_PUBLIC_AUTH_MODE possui um valor invalido.');
  }
}

function inferDefaultAuthMode(apiBaseUrl: string): RuntimeEnvironment['authMode'] {
  const origin = tryGetOrigin(apiBaseUrl);
  if (!origin) {
    return 'jwt';
  }

  const hostname = new URL(origin).hostname.toLowerCase();
  return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1'
    ? 'development-headers'
    : 'jwt';
}

function resolveDefaultUserRole(value: string | undefined): RuntimeEnvironment['userRole'] {
  switch (value) {
    case 'TenantUser':
    case 'Analyst':
    case 'TenantAdmin':
    case 'PlatformAdmin':
    case 'McpClient':
      return value;
    default:
      throw new Error('NEXT_PUBLIC_DEFAULT_USER_ROLE possui um valor invalido.');
  }
}

function resolveBoolean(value: string, key: string) {
  const normalized = value.trim().toLowerCase();
  if (normalized === 'true') {
    return true;
  }

  if (normalized === 'false') {
    return false;
  }

  throw new Error(`${key} deve ser 'true' ou 'false'.`);
}

function resolveConnectSrcOrigins(value: string, apiBaseUrl: string) {
  const origins = new Set<string>();

  const apiOrigin = tryGetOrigin(apiBaseUrl);
  if (!apiOrigin) {
    throw new Error('NEXT_PUBLIC_API_BASE_URL contem uma origem invalida.');
  }

  addLoopbackAliases(origins, apiOrigin);

  for (const item of value.split(',')) {
    const origin = tryGetOrigin(item);
    if (!origin) {
      throw new Error('NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS contem uma origem invalida.');
    }

    addLoopbackAliases(origins, origin);
  }

  if (origins.size === 0) {
    throw new Error('NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS deve conter ao menos uma origem valida.');
  }

  return Array.from(origins);
}

function requireEnv(key: PublicEnvKey) {
  const value = readEnvValue(key);
  if (!value?.trim()) {
    throw new Error(`${key} e obrigatoria no web/.env.`);
  }

  return value.trim();
}

function readOptionalEnv(key: PublicEnvKey) {
  return readEnvValue(key)?.trim() ?? '';
}

function readEnvValue(key: PublicEnvKey) {
  const value = publicEnv[key]?.trim();
  if (value) {
    return value;
  }

  if (process.env.NODE_ENV !== 'production') {
    return developmentEnvFallback[key] ?? '';
  }

  return '';
}

function tryGetOrigin(value: string) {
  try {
    return new URL(value.trim()).origin;
  } catch {
    return null;
  }
}

function addLoopbackAliases(origins: Set<string>, origin: string) {
  const parsed = new URL(origin);
  const normalizedHost = parsed.hostname.toLowerCase();
  const isLoopback = normalizedHost === 'localhost' || normalizedHost === '127.0.0.1' || normalizedHost === '::1';

  if (!isLoopback || normalizedHost !== '::1') {
    origins.add(parsed.origin);
  }

  if (!isLoopback) {
    return;
  }

  const port = parsed.port;
  const aliases = ['localhost', '127.0.0.1'];
  for (const host of aliases) {
    const portSuffix = port ? `:${port}` : '';
    origins.add(`${parsed.protocol}//${host}${portSuffix}`);
  }
}
