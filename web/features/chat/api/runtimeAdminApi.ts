import { apiRequest } from '@/lib/http';
import type { RuntimeEnvironment } from '@/types/app';
import type { OperationalAuditFeed, OperationalAuditQuery, RagRuntimeSettings } from '@/features/chat/types/chat';

export async function getRagRuntimeSettings(env: RuntimeEnvironment) {
  return apiRequest<RagRuntimeSettings>(env, '/api/v1/admin/rag-runtime', {
    method: 'GET'
  });
}

export async function updateRagRuntimeSettings(env: RuntimeEnvironment, request: RagRuntimeSettings) {
  return apiRequest<RagRuntimeSettings>(env, '/api/v1/admin/rag-runtime', {
    method: 'PUT',
    jsonBody: request
  });
}

export async function getOperationalAuditFeed(env: RuntimeEnvironment, query: OperationalAuditQuery) {
  const searchParams = new URLSearchParams();

  if (query.category) {
    searchParams.set('category', query.category);
  }

  if (query.status) {
    searchParams.set('status', query.status);
  }

  if (query.fromUtc) {
    searchParams.set('fromUtc', query.fromUtc);
  }

  if (query.toUtc) {
    searchParams.set('toUtc', query.toUtc);
  }

  if (query.cursor) {
    searchParams.set('cursor', query.cursor);
  }

  searchParams.set('limit', String(query.limit));

  return apiRequest<OperationalAuditFeed>(env, `/api/v1/platform/operational-audit?${searchParams.toString()}`, {
    method: 'GET'
  });
}