import { describe, expect, it } from 'vitest';
import { buildLocalStrategyChunks } from '@/features/documents/viewer/chunking';
import type { DocumentNode, UnifiedDocumentModel } from '@/features/documents/viewer/types';
import { annotateTextRanges, createDocumentMetadata, extractPlainText, normalizeTextForMatching } from '@/features/documents/viewer/utils';

function createModel(root: DocumentNode[]): UnifiedDocumentModel {
  const annotatedRoot = annotateTextRanges(root);
  const plainText = extractPlainText(annotatedRoot);

  return {
    metadata: createDocumentMetadata({
      documentId: 'doc-1',
      title: 'Documento de teste',
      originalFileName: 'documento-teste.txt',
      contentType: 'text/plain',
      format: 'txt'
    }),
    root: annotatedRoot,
    outline: [],
    assets: [],
    plainText,
    normalizedText: normalizeTextForMatching(plainText)
  };
}

describe('buildLocalStrategyChunks', () => {
  it('mantem blocos estruturais inteiros no modo por limite', () => {
    const model = createModel([
      {
        id: 'page-1',
        kind: 'page',
        sourceMap: { sourceType: 'page', pageNumber: 1 },
        children: [
          {
            id: 'heading-1',
            kind: 'heading',
            level: 2,
            sourceMap: { sourceType: 'heading', pageNumber: 1 },
            children: [{ id: 'heading-text-1', kind: 'text', text: 'Resumo', sourceMap: { sourceType: 'heading', pageNumber: 1 } }]
          },
          {
            id: 'paragraph-1',
            kind: 'paragraph',
            sourceMap: { sourceType: 'paragraph', pageNumber: 1 },
            children: [{ id: 'paragraph-text-1', kind: 'text', text: 'Primeiro bloco com contexto estrutural.', sourceMap: { sourceType: 'paragraph', pageNumber: 1 } }]
          },
          {
            id: 'table-1',
            kind: 'table',
            sourceMap: { sourceType: 'table', pageNumber: 1 },
            children: [
              {
                id: 'row-1',
                kind: 'table-row',
                sourceMap: { sourceType: 'table', pageNumber: 1 },
                children: [
                  {
                    id: 'cell-1',
                    kind: 'table-cell',
                    sourceMap: { sourceType: 'table', pageNumber: 1 },
                    children: [{ id: 'cell-text-1', kind: 'text', text: 'Linha 1', sourceMap: { sourceType: 'table', pageNumber: 1 } }]
                  }
                ]
              }
            ]
          }
        ]
      }
    ]);

    const chunks = buildLocalStrategyChunks(model, 'doc-1', 'documento-teste.txt', 'character-limit', {
      maxCharacters: 20,
      overlapBlocks: 0
    });

    expect(chunks.length).toBeGreaterThanOrEqual(2);
    expect(chunks.some((chunk) => chunk.formattedContent.some((node) => node.kind === 'table'))).toBe(true);
  });

  it('agrupa por pagina quando a estrategia e page', () => {
    const model = createModel([
      {
        id: 'page-1',
        kind: 'page',
        sourceMap: { sourceType: 'page', pageNumber: 1 },
        children: [{ id: 'page-1-paragraph', kind: 'paragraph', sourceMap: { sourceType: 'paragraph', pageNumber: 1 }, children: [{ id: 'page-1-text', kind: 'text', text: 'Conteudo da pagina um.', sourceMap: { sourceType: 'paragraph', pageNumber: 1 } }] }]
      },
      {
        id: 'page-2',
        kind: 'page',
        sourceMap: { sourceType: 'page', pageNumber: 2 },
        children: [{ id: 'page-2-paragraph', kind: 'paragraph', sourceMap: { sourceType: 'paragraph', pageNumber: 2 }, children: [{ id: 'page-2-text', kind: 'text', text: 'Conteudo da pagina dois.', sourceMap: { sourceType: 'paragraph', pageNumber: 2 } }] }]
      }
    ]);

    const chunks = buildLocalStrategyChunks(model, 'doc-1', 'documento-teste.txt', 'page', {
      maxCharacters: 500,
      overlapBlocks: 0
    });

    expect(chunks).toHaveLength(2);
    expect(chunks[0].sourceMap.pageNumber).toBe(1);
    expect(chunks[1].sourceMap.pageNumber).toBe(2);
  });
});