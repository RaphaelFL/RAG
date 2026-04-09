import { ApiError, apiRequest } from '@/lib/http';
import { buildDocumentContentUrl, buildProxyUrl } from '@/lib/runtimeEnvironment';
import type { RuntimeEnvironment } from '@/types/app';
import type { BulkReindexResponse, DocumentChunkEmbedding, DocumentDetails, DocumentInspection, DocumentMetadataSuggestion, DocumentStatus, DocumentTextPreview, UploadDocumentResponse } from '@/features/documents/types/documents';

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

export async function listDocuments(env: RuntimeEnvironment) {
  return apiRequest<DocumentDetails[]>(env, '/api/v1/documents', {
    method: 'GET'
  });
}

export async function getDocumentInspection(env: RuntimeEnvironment, documentId: string) {
  return apiRequest<DocumentInspection>(env, `/api/v1/documents/${documentId}/inspection`, {
    method: 'GET'
  });
}

export async function getDocumentInspectionPage(
  env: RuntimeEnvironment,
  documentId: string,
  input: {
    search?: string;
    page?: number;
    pageSize?: number;
  }
) {
  const searchParams = new URLSearchParams();

  if (input.search?.trim()) {
    searchParams.set('search', input.search.trim());
  }

  if (input.page) {
    searchParams.set('page', String(input.page));
  }

  if (input.pageSize) {
    searchParams.set('pageSize', String(input.pageSize));
  }

  const query = searchParams.toString();
  const path = query
    ? `/api/v1/documents/${documentId}/inspection?${query}`
    : `/api/v1/documents/${documentId}/inspection`;

  return apiRequest<DocumentInspection>(env, path, {
    method: 'GET'
  });
}

export async function getDocumentChunkEmbedding(env: RuntimeEnvironment, documentId: string, chunkId: string) {
  return apiRequest<DocumentChunkEmbedding>(env, `/api/v1/documents/${documentId}/chunks/${encodeURIComponent(chunkId)}/embedding`, {
    method: 'GET'
  });
}

export async function getDocumentTextPreview(env: RuntimeEnvironment, documentId: string) {
  return apiRequest<DocumentTextPreview>(env, `/api/v1/documents/${documentId}/text-preview`, {
    method: 'GET'
  });
}

export function getDocumentContentUrl(documentId: string, pageNumber?: number | null) {
  return buildDocumentContentUrl(documentId, pageNumber);
}

export async function reindexDocument(env: RuntimeEnvironment, documentId: string, fullReindex: boolean) {
  return apiRequest<{ documentId: string; status: DocumentStatus; chunksReindexed: number; jobId?: string | null }>(
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
