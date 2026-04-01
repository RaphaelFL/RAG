'use client';

import { useState } from 'react';
import { ApiError } from '@/lib/http';
import { getDocument, reindexDocument, uploadDocument } from '@/features/documents/api/documentsApi';
import type { RuntimeEnvironment } from '@/types/app';
import type { DocumentDetails, DocumentUploadModel } from '@/features/documents/types/documents';

export function useDocumentUploads(environment: RuntimeEnvironment) {
  const [uploads, setUploads] = useState<DocumentUploadModel[]>([]);
  const [error, setError] = useState<string | null>(null);

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
    setUploads((current) => [
      {
        localId,
        fileName: input.file.name,
        status: 'sending'
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

      setUploads((current) =>
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
      setUploads((current) =>
        current.map((entry) =>
          entry.localId === localId
            ? { ...entry, status: 'failed', error: message }
            : entry
        )
      );
    }
  }

  async function triggerReindex(documentId: string, fullReindex: boolean) {
    const response = await reindexDocument(environment, documentId, fullReindex);
    setUploads((current) =>
      current.map((entry) =>
        entry.documentId === documentId
          ? { ...entry, status: response.status, ingestionJobId: response.jobId ?? entry.ingestionJobId }
          : entry
      )
    );

    const target = uploads.find((entry) => entry.documentId === documentId);
    await pollDocument(documentId, target?.localId);
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
    setUploads((current) => {
      const existing = current.find((entry) => entry.documentId === details.documentId || entry.localId === localId);

      if (!existing) {
        return [
          {
            localId: localId ?? crypto.randomUUID(),
            documentId: details.documentId,
            fileName: details.title,
            status: details.status,
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
              details
            }
          : entry
      );
    });
  }

  return {
    uploads,
    error,
    submitUpload,
    triggerReindex
  };
}
