import { apiRequest } from '@/lib/http';
import type { RuntimeEnvironment } from '@/types/app';
import type { RagRuntimeSettings } from '@/features/chat/types/chat';

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