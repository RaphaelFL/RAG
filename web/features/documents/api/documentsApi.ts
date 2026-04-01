import { ApiError, apiRequest, buildUrl } from '@/lib/http';
import type { RuntimeEnvironment } from '@/types/app';
import type { DocumentDetails, UploadDocumentResponse } from '@/features/documents/types/documents';

export async function uploadDocument(
  env: RuntimeEnvironment,
  input: {
    file: File;
    documentTitle?: string;
    category?: string;
    categories?: string[];
    tags?: string[];
    source?: string;
  }
) {
  const formData = new FormData();
  formData.append('file', input.file);

  if (input.documentTitle) {
    formData.append('documentTitle', input.documentTitle);
  }

  if (input.category) {
    formData.append('category', input.category);
  }

  if (input.categories?.length) {
    formData.append('categories', input.categories.join(','));
  }

  if (input.tags?.length) {
    formData.append('tags', input.tags.join(','));
  }

  if (input.source) {
    formData.append('source', input.source);
  }

  const response = await fetch(buildUrl(env.apiBaseUrl, '/api/v1/documents/ingest'), {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${env.token}`,
      'X-Tenant-Id': env.tenantId,
      'X-User-Id': env.userId,
      'X-User-Role': env.userRole
    },
    body: formData
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => undefined);
    throw new ApiError(
      typeof payload?.message === 'string' ? payload.message : `HTTP ${response.status}`,
      response.status,
      payload
    );
  }

  return response.json() as Promise<UploadDocumentResponse>;
}

export async function getDocument(env: RuntimeEnvironment, documentId: string) {
  return apiRequest<DocumentDetails>(env, `/api/v1/documents/${documentId}`, {
    method: 'GET'
  });
}

export async function reindexDocument(env: RuntimeEnvironment, documentId: string, fullReindex: boolean) {
  return apiRequest<{ documentId: string; status: string; chunksReindexed: number; jobId?: string | null }>(
    env,
    `/api/v1/documents/${documentId}/reindex`,
    {
      method: 'POST',
      jsonBody: {
        documentId,
        fullReindex
      }
    }
  );
}
