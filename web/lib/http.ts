import type { RuntimeEnvironment } from '@/types/app';

export class ApiError extends Error {
  status: number;
  payload?: unknown;

  constructor(message: string, status: number, payload?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.payload = payload;
  }
}

type RequestOptions = Omit<RequestInit, 'body' | 'headers'> & {
  body?: BodyInit | null;
  jsonBody?: unknown;
  headers?: Record<string, string>;
};

export async function apiRequest<T>(
  env: RuntimeEnvironment,
  path: string,
  options: RequestOptions = {}
): Promise<T> {
  const requestBody = options.jsonBody === undefined ? options.body : JSON.stringify(options.jsonBody);

  const response = await fetch(buildUrl(env.apiBaseUrl, path), {
    ...options,
    headers: buildHeaders(env, options.headers),
    body: requestBody
  });

  if (!response.ok) {
    const payload = await tryParseJson(response);
    const message = resolveApiErrorMessage(payload, response.status);
    throw new ApiError(message, response.status, payload);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export function buildHeaders(env: RuntimeEnvironment, headers?: Record<string, string>) {
  const resolvedHeaders: Record<string, string> = {
    'X-Tenant-Id': env.tenantId,
    'X-User-Id': env.userId,
    'X-User-Role': env.userRole,
    'Content-Type': 'application/json',
    ...headers
  };

  if (env.token.trim()) {
    resolvedHeaders.Authorization = `Bearer ${env.token}`;
  }

  return resolvedHeaders;
}

export function buildUrl(baseUrl: string, path: string) {
  const normalizedBaseUrl = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBaseUrl}${normalizedPath}`;
}

async function tryParseJson(response: Response) {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

function resolveApiErrorMessage(payload: unknown, status: number) {
  if (typeof payload === 'object' && payload && 'message' in payload) {
    const message = (payload as { message?: unknown }).message;
    return typeof message === 'string' && message.trim() ? message : 'Erro na API';
  }

  return `HTTP ${status}`;
}
