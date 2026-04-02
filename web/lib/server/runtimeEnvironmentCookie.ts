import { RUNTIME_ENV_COOKIE_NAME, normalizeRuntimeEnvironment } from '@/lib/runtimeEnvironment';
import type { RuntimeEnvironment } from '@/types/app';

export function serializeRuntimeEnvironmentCookie(environment: RuntimeEnvironment) {
  return Buffer.from(JSON.stringify(environment), 'utf8').toString('base64url');
}

export function readRuntimeEnvironmentCookie(rawValue?: string) {
  if (!rawValue) {
    return null;
  }

  try {
    const json = Buffer.from(rawValue, 'base64url').toString('utf8');
    return normalizeRuntimeEnvironment(JSON.parse(json) as unknown);
  } catch {
    return null;
  }
}

export function createRuntimeEnvironmentCookieOptions() {
  return {
    name: RUNTIME_ENV_COOKIE_NAME,
    httpOnly: true,
    sameSite: 'lax' as const,
    secure: process.env.NODE_ENV === 'production',
    path: '/'
  };
}