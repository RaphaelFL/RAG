'use client';

import { useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/http';
import { bulkReindexDocuments, getDocument, reindexDocument, suggestDocumentMetadata, uploadDocument } from '@/features/documents/api/documentsApi';
import type { RuntimeEnvironment } from '@/types/app';
import type { BulkReindexResponse, DocumentDetails, DocumentStatus, DocumentUploadModel } from '@/features/documents/types/documents';

const TERMINAL_DOCUMENT_STATUSES = new Set<DocumentStatus>(['Indexed', 'Failed', 'Archived', 'Deleted']);

function shouldAutoRefresh(status: DocumentStatus | 'sending') {
  return status !== 'sending' && !TERMINAL_DOCUMENT_STATUSES.has(status);
}

function getAutoRefreshDelayMs(status: DocumentStatus, attempt: number) {
  switch (status) {
    case 'Queued':
    case 'ReindexPending':
      return Math.min(30000, 8000 + attempt * 4000);
    case 'Parsing':
    case 'OcrProcessing':
      return Math.min(20000, 5000 + attempt * 3000);
    case 'Chunking':
    case 'Embedding':
      return Math.min(45000, 12000 + attempt * 5000);
    case 'Indexing':
      return Math.min(20000, 6000 + attempt * 3000);
    default:
      return 10000;
  }
}

function getStatusMessage(status: DocumentStatus) {
  switch (status) {
    case 'Queued':
      return 'Aceito pelo backend e colocado na fila interna. O status sera atualizado automaticamente.';
    case 'ReindexPending':
      return 'Reindexacao aceita pelo backend. O status sera atualizado automaticamente.';
    case 'Parsing':
    case 'OcrProcessing':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
      return 'Processamento em andamento no backend.';
    default:
      return undefined;
  }
}

export function useDocumentUploads(environment: RuntimeEnvironment, conversationSessionId: string) {
  const [allUploads, setAllUploads] = useState<DocumentUploadModel[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [lastBulkReindex, setLastBulkReindex] = useState<BulkReindexResponse | null>(null);
  const refreshTimersRef = useRef(new Map<string, ReturnType<typeof globalThis.setTimeout>>());
  const refreshAttemptsRef = useRef(new Map<string, number>());
  const inFlightRefreshesRef = useRef(new Set<string>());

  function clearRefreshTimer(documentId: string) {
    const timer = refreshTimersRef.current.get(documentId);
    if (timer) {
      globalThis.clearTimeout(timer);
      refreshTimersRef.current.delete(documentId);
    }
  }

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
                status: result.status,
                statusMessage: getStatusMessage(result.status),
                error: undefined
              }
            : entry
        )
      );
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Falha no upload';
      setError(message);
      setAllUploads((current) =>
        current.map((entry) =>
          entry.localId === localId
            ? { ...entry, status: 'Failed', error: message }
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
          ? {
              ...entry,
              status: response.status,
              ingestionJobId: response.jobId ?? entry.ingestionJobId,
              statusMessage: getStatusMessage(response.status),
              error: undefined
            }
          : entry
      )
    );
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
              ingestionJobId: response.jobId,
              statusMessage: getStatusMessage('ReindexPending')
            }
          : entry
      )
    );
  }

  function syncDetails(details: DocumentDetails) {
    setAllUploads((current) =>
      current.map((entry) =>
        entry.documentId === details.documentId
          ? {
              ...entry,
              fileName: details.title,
              status: details.status,
              logicalTitle: details.title,
              category: details.metadata.category ?? details.metadata.categories[0] ?? entry.category,
              tags: details.metadata.tags.length > 0 ? details.metadata.tags : entry.tags,
              statusMessage: getStatusMessage(details.status),
              error: details.status === 'Failed' ? entry.error : undefined,
              details
            }
          : entry
      )
    );
  }

  async function autoRefreshDocumentStatus(documentId: string) {
    if (inFlightRefreshesRef.current.has(documentId)) {
      return;
    }

    inFlightRefreshesRef.current.add(documentId);

    try {
      const details = await getDocument(environment, documentId);

      if (shouldAutoRefresh(details.status)) {
        refreshAttemptsRef.current.set(documentId, (refreshAttemptsRef.current.get(documentId) ?? 0) + 1);
      } else {
        refreshAttemptsRef.current.delete(documentId);
        clearRefreshTimer(documentId);
      }

      syncDetails(details);
    } catch {
      refreshAttemptsRef.current.set(documentId, (refreshAttemptsRef.current.get(documentId) ?? 0) + 1);
      setAllUploads((current) =>
        current.map((entry) =>
          entry.documentId === documentId
            ? {
                ...entry,
                statusMessage: 'Nao foi possivel consultar o status agora. Nova tentativa automatica em instantes.'
              }
            : entry
        )
      );
    } finally {
      inFlightRefreshesRef.current.delete(documentId);
    }
  }

  useEffect(() => {
    for (const entry of allUploads) {
      if (entry.conversationSessionId !== conversationSessionId || !entry.documentId) {
        continue;
      }

      if (!shouldAutoRefresh(entry.status)) {
        clearRefreshTimer(entry.documentId);
        refreshAttemptsRef.current.delete(entry.documentId);
        continue;
      }

      if (entry.status === 'sending') {
        continue;
      }

      if (refreshTimersRef.current.has(entry.documentId) || inFlightRefreshesRef.current.has(entry.documentId)) {
        continue;
      }

      const attempt = refreshAttemptsRef.current.get(entry.documentId) ?? 0;
      const delay = getAutoRefreshDelayMs(entry.status, attempt);
      const timer = globalThis.setTimeout(() => {
        refreshTimersRef.current.delete(entry.documentId as string);
        void autoRefreshDocumentStatus(entry.documentId as string);
      }, delay);

      refreshTimersRef.current.set(entry.documentId, timer);
    }

    for (const documentId of refreshTimersRef.current.keys()) {
      const stillTracked = allUploads.some(
        (entry) => entry.conversationSessionId === conversationSessionId && entry.documentId === documentId && shouldAutoRefresh(entry.status)
      );

      if (!stillTracked) {
        clearRefreshTimer(documentId);
        refreshAttemptsRef.current.delete(documentId);
      }
    }
  }, [allUploads, conversationSessionId, environment]);

  useEffect(() => {
    return () => {
      for (const timer of refreshTimersRef.current.values()) {
        globalThis.clearTimeout(timer);
      }

      refreshTimersRef.current.clear();
      refreshAttemptsRef.current.clear();
      inFlightRefreshesRef.current.clear();
    };
  }, []);

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
