'use client';

import { useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/http';
import { bulkReindexDocuments, getDocument, reindexDocument, suggestDocumentMetadata, uploadDocument } from '@/features/documents/api/documentsApi';
import { parseDocumentBlob } from '@/features/documents/viewer/parsers';
import { extractPlainText } from '@/features/documents/viewer/utils';
import type { DocumentNode } from '@/features/documents/viewer/types';
import type { RuntimeEnvironment } from '@/types/app';
import type { BulkReindexResponse, DocumentDetails, DocumentStatus, DocumentUploadModel } from '@/features/documents/types/documents';

const TERMINAL_DOCUMENT_STATUSES = new Set<DocumentStatus>(['Indexed', 'Failed', 'Archived', 'Deleted']);
const PDF_MIME_TYPE = 'application/pdf';

type UploadDocumentExtraction = {
  extractedText: string;
  extractedPages: Array<{
    pageNumber: number;
    text: string;
  }>;
};

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

function getExistingDocumentId(error: ApiError) {
  if (error.status !== 409 || typeof error.payload !== 'object' || error.payload === null) {
    return null;
  }

  const payload = error.payload as {
    code?: unknown;
    details?: Record<string, unknown>;
  };

  if (payload.code !== 'document_conflict') {
    return null;
  }

  const rawValue = payload.details?.existingDocumentId;
  if (!Array.isArray(rawValue) || typeof rawValue[0] !== 'string' || !rawValue[0].trim()) {
    return null;
  }

  return rawValue[0];
}

function getDuplicateDocumentStatusMessage(status: DocumentStatus) {
  const baseMessage = 'Documento com o mesmo conteudo ja existe para este tenant. O registro existente foi carregado.';
  const processingMessage = getStatusMessage(status);
  return processingMessage ? `${baseMessage} ${processingMessage}` : baseMessage;
}

export function useDocumentUploads(environment: RuntimeEnvironment, conversationSessionId: string) {
  const [allUploads, setAllUploads] = useState<DocumentUploadModel[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [lastBulkReindex, setLastBulkReindex] = useState<BulkReindexResponse | null>(null);
  const refreshTimersRef = useRef(new Map<string, ReturnType<typeof globalThis.setTimeout>>());
  const refreshAttemptsRef = useRef(new Map<string, number>());
  const inFlightRefreshesRef = useRef(new Set<string>());
  const extractionCacheRef = useRef(new WeakMap<File, Promise<UploadDocumentExtraction | null>>());

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
      const extraction = await getClientExtraction(file);
      return await suggestDocumentMetadata(environment, {
        file,
        extractedText: extraction?.extractedText,
        extractedPages: extraction?.extractedPages
      });
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
      const extraction = await getClientExtraction(input.file);
      const result = await uploadDocument(environment, {
        file: input.file,
        documentTitle: input.title,
        category: input.category,
        categories: input.categories,
        tags: input.tags,
        source: input.source,
        extractedText: extraction?.extractedText,
        extractedPages: extraction?.extractedPages
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
      if (error instanceof ApiError) {
        const existingDocumentId = getExistingDocumentId(error);

        if (existingDocumentId) {
          try {
            const details = await getDocument(environment, existingDocumentId);
            setAllUploads((current) =>
              current.map((entry) =>
                entry.localId === localId
                  ? {
                      ...entry,
                      fileName: details.originalFileName,
                      documentId: details.documentId,
                      status: details.status,
                      logicalTitle: details.title,
                      category: details.metadata.category ?? details.metadata.categories[0] ?? entry.category,
                      tags: details.metadata.tags.length > 0 ? details.metadata.tags : entry.tags,
                      statusMessage: getDuplicateDocumentStatusMessage(details.status),
                      error: undefined,
                      details
                    }
                  : entry
              )
            );
            return;
          } catch {
            // Fall through to the generic error path when the existing document cannot be loaded.
          }
        }
      }

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

  function getClientExtraction(file: File) {
    const cached = extractionCacheRef.current.get(file);
    if (cached) {
      return cached;
    }

    const pending = extractClientExtraction(file);
    extractionCacheRef.current.set(file, pending);
    return pending;
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

async function extractClientExtraction(file: File): Promise<UploadDocumentExtraction | null> {
  if (!isPdfFile(file)) {
    return null;
  }

  try {
    const parsed = await parseDocumentBlob({
      documentId: crypto.randomUUID(),
      title: file.name,
      originalFileName: file.name,
      contentType: file.type || PDF_MIME_TYPE,
      blob: file
    });
    const extractedPages = parsed.model.root
      .filter((node): node is DocumentNode & { kind: 'page' } => node.kind === 'page')
      .map((node, index) => ({
        pageNumber: node.sourceMap?.pageNumber ?? index + 1,
        text: extractPlainText(node.children).trim()
      }))
      .filter((page) => page.text.length > 0);
    const extractedText = parsed.model.plainText.trim();

    if (!extractedText) {
      return null;
    }

    return {
      extractedText,
      extractedPages
    };
  } catch {
    return null;
  }
}

function isPdfFile(file: File) {
  return file.type === PDF_MIME_TYPE || file.name.toLowerCase().endsWith('.pdf');
}
