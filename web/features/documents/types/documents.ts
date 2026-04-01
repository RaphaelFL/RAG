export type UploadDocumentResponse = {
  documentId: string;
  status: string;
  ingestionJobId: string;
  timestampUtc: string;
  createdAtUtc: string;
};

export type DocumentDetails = {
  documentId: string;
  title: string;
  status: string;
  version: number;
  contentType: string;
  source?: string | null;
  lastJobId?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  metadata: {
    category?: string | null;
    tags: string[];
    categories: string[];
    externalId?: string | null;
    accessPolicy?: string | null;
  };
};

export type DocumentUploadModel = {
  localId: string;
  fileName: string;
  documentId?: string;
  ingestionJobId?: string;
  status: string;
  error?: string;
  details?: DocumentDetails;
};
