import { ApiError, apiRequest } from '@/lib/http';
import { buildProxyUrl } from '@/lib/runtimeEnvironment';
import type { RuntimeEnvironment } from '@/types/app';
import type { BulkReindexResponse, DocumentDetails, DocumentMetadataSuggestion, UploadDocumentResponse } from '@/features/documents/types/documents';

export async function suggestDocumentMetadata(
  env: RuntimeEnvironment,
  input: {
    file: File;
  }
) {
  const formData = new FormData();
  formData.append('file', input.file);

  const response = await fetch(buildProxyUrl('/api/v1/documents/suggest-metadata'), {
    method: 'POST',
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

  return response.json() as Promise<DocumentMetadataSuggestion>;
}

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

  const response = await fetch(buildProxyUrl('/api/v1/documents/ingest'), {
    method: 'POST',
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

export async function bulkReindexDocuments(
  env: RuntimeEnvironment,
  input: {
    documentIds?: string[];
    includeAllTenantDocuments?: boolean;
    mode: 'incremental' | 'full';
    reason?: string;
  }
) {
  return apiRequest<BulkReindexResponse>(env, '/api/v1/documents/reindex', {
    method: 'POST',
    jsonBody: {
      documentIds: input.documentIds ?? [],
      includeAllTenantDocuments: Boolean(input.includeAllTenantDocuments),
      mode: input.mode,
      reason: input.reason
    }
  });
}
