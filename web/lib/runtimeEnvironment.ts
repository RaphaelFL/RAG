import { createDefaultEnvironment } from '@/lib/publicEnv';
import type { AuthMode, RuntimeEnvironment, UserRole } from '@/types/app';

export const RUNTIME_ENV_COOKIE_NAME = 'rag-runtime-environment';
export const INTERNAL_PROXY_PREFIX = '/api/proxy';

const USER_ROLES: UserRole[] = ['TenantUser', 'Analyst', 'TenantAdmin', 'PlatformAdmin', 'McpClient'];
const AUTH_MODES: AuthMode[] = ['jwt', 'development-headers'];

export function buildProxyUrl(path: string) {
  return `${INTERNAL_PROXY_PREFIX}${normalizePath(path)}`;
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
    apiBaseUrl: readString(source.apiBaseUrl, defaults.apiBaseUrl, true),
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

function readAuthMode(value: unknown, fallback: AuthMode): AuthMode {
  return typeof value === 'string' && AUTH_MODES.includes(value as AuthMode)
    ? value as AuthMode
    : fallback;
}

function readUserRole(value: unknown, fallback: UserRole): UserRole {
  return typeof value === 'string' && USER_ROLES.includes(value as UserRole)
    ? value as UserRole
    : fallback;
}