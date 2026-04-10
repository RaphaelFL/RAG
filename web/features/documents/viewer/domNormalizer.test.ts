import { describe, expect, it } from 'vitest';
import { normalizeDomDocument } from '@/features/documents/viewer/domNormalizer';

describe('normalizeDomDocument', () => {
  it('ignora estilos embutidos para nao renderizar CSS como texto', () => {
    const root = document.createElement('div');
    const style = document.createElement('style');
    style.textContent = '.docx-wrapper { background: gray; } .docx p { margin: 0; }';

    const section = document.createElement('section');
    section.dataset.outlineKind = 'section';
    section.dataset.outlineLabel = 'Secao 1';
    section.dataset.sectionTitle = 'Secao 1';

    const paragraph = document.createElement('p');
    paragraph.textContent = 'Conteudo renderizado do documento';
    section.append(paragraph);
    root.append(style, section);

    const payload = normalizeDomDocument({
      input: {
        documentId: 'doc-1',
        title: 'Documento DOCX',
        originalFileName: 'documento.docx',
        contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        blob: new Blob([''])
      },
      root,
      format: 'docx',
      outlineKind: 'section'
    });

    expect(payload.model.plainText).toContain('Conteudo renderizado do documento');
    expect(payload.model.plainText).not.toContain('.docx-wrapper');
    expect(payload.model.root).toHaveLength(1);
  });
});