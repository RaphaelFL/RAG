'use client';

import { useState } from 'react';
import { ApiError } from '@/lib/http';
import { bulkReindexDocuments, getDocument, reindexDocument, suggestDocumentMetadata, uploadDocument } from '@/features/documents/api/documentsApi';
import type { RuntimeEnvironment } from '@/types/app';
import type { BulkReindexResponse, DocumentDetails, DocumentUploadModel } from '@/features/documents/types/documents';

export function useDocumentUploads(environment: RuntimeEnvironment, conversationSessionId: string) {
  const [allUploads, setAllUploads] = useState<DocumentUploadModel[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [lastBulkReindex, setLastBulkReindex] = useState<BulkReindexResponse | null>(null);

  async function suggestUploadMetadata(file: File) {
    setError(null);

    try {
      return await suggestDocumentMetadata(environment, { file });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Falha ao analisar o documento';
      setError(message);
      throw error;
    }
  }

  async function submitUpload(input: {
    file: File;
    title?: string;
    category?: string;
    categories?: string[];
    tags?: string[];
    source?: string;
  }) {
    const localId = crypto.randomUUID();
    setError(null);
    setAllUploads((current) => [
      {
        localId,
        conversationSessionId,
        fileName: input.file.name,
        status: 'sending',
        logicalTitle: input.title,
        category: input.category ?? input.categories?.[0],
        tags: input.tags ?? []
      },
      ...current
    ]);

    try {
      const result = await uploadDocument(environment, {
        file: input.file,
        documentTitle: input.title,
        category: input.category,
        categories: input.categories,
        tags: input.tags,
        source: input.source
      });

      setAllUploads((current) =>
        current.map((entry) =>
          entry.localId === localId
            ? {
                ...entry,
                documentId: result.documentId,
                ingestionJobId: result.ingestionJobId,
                status: result.status
              }
            : entry
        )
      );

      await pollDocument(result.documentId, localId);
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Falha no upload';
      setError(message);
      setAllUploads((current) =>
        current.map((entry) =>
          entry.localId === localId
            ? { ...entry, status: 'failed', error: message }
            : entry
        )
      );
    }
  }

  async function triggerReindex(documentId: string, fullReindex: boolean) {
    setError(null);
    const response = await reindexDocument(environment, documentId, fullReindex);
    setAllUploads((current) =>
      current.map((entry) =>
        entry.documentId === documentId
          ? { ...entry, status: response.status, ingestionJobId: response.jobId ?? entry.ingestionJobId }
          : entry
      )
    );

    await pollDocument(documentId);
  }

  async function triggerTenantFullReindex() {
    setError(null);
    const response = await bulkReindexDocuments(environment, {
      includeAllTenantDocuments: true,
      mode: 'full',
      reason: 'apply-semantic-chunking-upgrade'
    });

    setLastBulkReindex(response);
    setAllUploads((current) =>
      current.map((entry) =>
        entry.documentId
          ? {
              ...entry,
              status: 'ReindexPending',
              ingestionJobId: response.jobId
            }
          : entry
      )
    );
  }

  async function pollDocument(documentId: string, localId?: string) {
    for (let attempt = 0; attempt < 40; attempt += 1) {
      const details = await getDocument(environment, documentId);
      syncDetails(details, localId);

      if (['Indexed', 'Failed'].includes(details.status)) {
        return details;
      }

      await new Promise((resolve) => globalThis.setTimeout(resolve, 350));
    }

    throw new Error('Timeout aguardando indexacao do documento.');
  }

  function syncDetails(details: DocumentDetails, localId?: string) {
    setAllUploads((current) => {
      const existing = current.find((entry) => entry.documentId === details.documentId || entry.localId === localId);

      if (!existing) {
        return [
          {
            localId: localId ?? crypto.randomUUID(),
            conversationSessionId,
            documentId: details.documentId,
            fileName: details.title,
            status: details.status,
            logicalTitle: details.title,
            category: details.metadata.category ?? details.metadata.categories[0],
            tags: details.metadata.tags,
            details
          },
          ...current
        ];
      }

      return current.map((entry) =>
        entry.documentId === details.documentId || entry.localId === localId
          ? {
              ...entry,
              documentId: details.documentId,
              fileName: details.title,
              status: details.status,
              logicalTitle: details.title,
              category: details.metadata.category ?? details.metadata.categories[0] ?? entry.category,
              tags: details.metadata.tags.length > 0 ? details.metadata.tags : entry.tags,
              details
            }
          : entry
      );
    });
  }

  const uploads = allUploads.filter((entry) => entry.conversationSessionId === conversationSessionId);

  return {
    uploads,
    error,
    lastBulkReindex,
    suggestUploadMetadata,
    submitUpload,
    triggerReindex,
    triggerTenantFullReindex
  };
}
