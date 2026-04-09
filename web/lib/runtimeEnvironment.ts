import { createDefaultEnvironment } from '@/lib/publicEnv';
import type { AuthMode, RuntimeEnvironment, UserRole } from '@/types/app';

export const RUNTIME_ENV_COOKIE_NAME = 'rag-runtime-environment';
export const INTERNAL_PROXY_PREFIX = '/api/proxy';

const USER_ROLES = new Set<UserRole>(['TenantUser', 'Analyst', 'TenantAdmin', 'PlatformAdmin', 'McpClient']);
const AUTH_MODES = new Set<AuthMode>(['jwt', 'development-headers']);

export function buildProxyUrl(path: string) {
  return `${INTERNAL_PROXY_PREFIX}${normalizePath(path)}`;
}

export function buildDocumentContentUrl(documentId: string, pageNumber?: number | null) {
  const baseUrl = buildProxyUrl(`/api/v1/documents/${documentId}/content`);
  return pageNumber && Number.isFinite(pageNumber) && pageNumber > 0
    ? `${baseUrl}#page=${pageNumber}`
    : baseUrl;
}

export function buildBackendUrl(baseUrl: string, path: string, search = '') {
  const normalizedBaseUrl = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  return `${normalizedBaseUrl}${normalizePath(path)}${search}`;
}

export function buildForwardedAuthHeaders(environment: RuntimeEnvironment) {
  const headers = new Headers();

  if (environment.authMode === 'development-headers') {
    headers.set('X-Tenant-Id', environment.tenantId);
    headers.set('X-User-Id', environment.userId);
    headers.set('X-User-Role', environment.userRole);
  }

  if (environment.token.trim()) {
    headers.set('Authorization', `Bearer ${environment.token}`);
  }

  return headers;
}

export function normalizeRuntimeEnvironment(value: unknown): RuntimeEnvironment {
  const defaults = createDefaultEnvironment();
  const source = isRecord(value) ? value : {};

  return {
    apiBaseUrl: readApiBaseUrl(source.apiBaseUrl, defaults.apiBaseUrl),
    token: readString(source.token, defaults.token),
    authMode: readAuthMode(source.authMode, defaults.authMode),
    tenantId: readString(source.tenantId, defaults.tenantId, true),
    userId: readString(source.userId, defaults.userId, true),
    userRole: readUserRole(source.userRole, defaults.userRole)
  };
}

function normalizePath(path: string) {
  return path.startsWith('/') ? path : `/${path}`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function readString(value: unknown, fallback: string, preserveFallbackWhenEmpty = false) {
  if (typeof value !== 'string') {
    return fallback;
  }

  const normalized = value.trim();
  if (!normalized && preserveFallbackWhenEmpty) {
    return fallback;
  }

  return normalized;
}

function readApiBaseUrl(value: unknown, fallback: string) {
  const normalized = readString(value, fallback, true);

  try {
    const url = new URL(normalized);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      return fallback;
    }

    return url.origin;
  } catch {
    return fallback;
  }
}

function readAuthMode(value: unknown, fallback: AuthMode): AuthMode {
  return typeof value === 'string' && AUTH_MODES.has(value as AuthMode)
    ? value as AuthMode
    : fallback;
}

function readUserRole(value: unknown, fallback: UserRole): UserRole {
  return typeof value === 'string' && USER_ROLES.has(value as UserRole)
    ? value as UserRole
    : fallback;
}