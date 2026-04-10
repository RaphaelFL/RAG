# Visualizador Unificado de Documentos

## Objetivo

Implementar uma visualizacao unificada para documento original e chunks, preservando ao maximo a estrutura real do trecho extraido sem transformar os chunks em plain text simplificado.

O foco da solucao e separar claramente:

- conteudo para visualizacao fiel;
- conteudo para retrieval/embedding;
- rastreabilidade do chunk ate a origem no arquivo.

## Estado Atual da Implementacao

A implementacao foi integrada ao fluxo existente de inspecao documental e hoje vive principalmente no frontend em:

- `web/features/documents/viewer/types.ts`
- `web/features/documents/viewer/utils.ts`
- `web/features/documents/viewer/domNormalizer.ts`
- `web/features/documents/viewer/parsers.ts`
- `web/features/documents/viewer/chunking.ts`
- `web/features/documents/viewer/DocumentNodeRenderer.tsx`
- `web/features/documents/viewer/UnifiedDocumentWorkspace.tsx`

A tela existente de detalhe foi adaptada em:

- `web/features/documents/components/DocumentInspectorDetailConsole.tsx`

O acesso ao arquivo original foi exposto no frontend em:

- `web/features/documents/api/documentsApi.ts`

## Arquitetura Final

### 1. Modelo intermediario unificado

O viewer nao renderiza chunks como string simples. Ele usa um modelo intermediario que representa documento e chunk por blocos estruturados.

Principais tipos:

- `DocumentMetadataModel`
- `UnifiedDocumentModel`
- `ViewerChunk`
- `ChunkFragment`
- `ChunkStyle`
- `ChunkSourceMap`
- `DocumentNode`
- `DocumentOutlineItem`
- `DocumentAsset`

Esse modelo permite:

- renderizacao fiel no browser;
- exportacao reutilizavel;
- destacamento visual da origem do chunk;
- reuso futuro no pipeline de RAG.

### 2. Pipeline por camadas

O fluxo foi dividido em camadas desacopladas:

1. `documentsApi.ts`
   - obtencao do blob do arquivo original e contratos de inspecao.
2. `parsers.ts`
   - parser especifico por formato.
3. `domNormalizer.ts`
   - converte DOM ou HTML gerado em `DocumentNode[]`.
4. `chunking.ts`
   - projeta chunks do backend sobre o documento estruturado ou gera estrategias locais.
5. `DocumentNodeRenderer.tsx`
   - renderiza o modelo intermediario em modo leitura limpa ou fidelidade maxima.
6. `UnifiedDocumentWorkspace.tsx`
   - orquestra UI, navegacao, exportacao, destaque de origem, zoom e seletor de estrategia.

### 3. Integracao com os chunks atuais

Os chunks hoje persistidos pelo backend continuam sendo a fonte principal no modo:

- `backend-synced`

O frontend tenta mapear o texto do chunk para o documento estruturado usando offsets normalizados e, quando encontra correspondencia, projeta o trecho real com a hierarquia e o estilo mais proximos possiveis.

Quando isso nao e possivel, aplica fallback rastreavel sem inventar conteudo.

### 4. Separacao entre visualizacao e retrieval

Cada chunk passa a carregar duas representacoes conceituais:

- `rawText` e `normalizedText`
  - base para retrieval, matching e embeddings.
- `formattedContent` e `formattedHtml`
  - base para renderizacao fiel ao usuario.

As duas apontam para o mesmo `chunkId` e compartilham `contentHash`, `sourceMap` e metadados de origem.

## UI Implementada

O workspace novo oferece:

- painel esquerdo com lista de chunks;
- area principal com alternancia entre documento completo e chunk selecionado;
- modo `Leitura limpa`;
- modo `Fidelidade maxima`;
- navegacao estrutural por pagina/secao/aba/slide quando disponivel;
- zoom;
- destaque visual da origem do chunk dentro do documento;
- download do documento original;
- download do chunk atual em HTML e JSON;
- exportacao de todos os chunks em JSON e HTML.

## Estrategias de Chunking Disponiveis

Hoje o viewer suporta estas estrategias no cliente:

- `backend-synced`
- `paragraph`
- `semantic-block`
- `page`
- `section`
- `heading`
- `character-limit`

Regras implementadas:

- preservacao de tabelas como blocos inteiros sempre que possivel;
- preservacao de listas e headings como unidades estruturais;
- preservacao de ancestry de heading na formacao dos chunks locais;
- overlap estrutural configuravel no modo por limite.

## Suporte por Formato

### PDF

Abordagem:

- `pdfjs-dist`
- agrupamento por pagina e linhas
- preservacao de coordenadas, fonte basica, quebras e spans

Preserva bem:

- texto;
- pagina de origem;
- coordenadas;
- alinhamento basico;
- estilo tipografico basico.

Limitacoes reais:

- nao recompõe completamente vetores, anotacoes ou imagens internas do PDF;
- layout muito complexo pode ser apenas aproximado;
- PDF escaneado continua dependendo de OCR em pipeline separado.

### DOCX

Abordagem:

- `docx-preview`
- render para HTML no browser
- normalizacao para `DocumentNode[]`

Preserva bem:

- headings;
- paragrafos;
- listas;
- tabelas;
- estilos inline;
- imagens embutidas.

Limitacoes reais:

- o resultado depende da fidelidade do renderer HTML do DOCX;
- recursos muito especificos de layout, secao ou pagina podem variar.

### XLSX

Abordagem:

- `xlsx`
- render por aba
- tabela HTML real por worksheet

Preserva bem:

- abas;
- linhas e colunas;
- tabelas reais;
- agrupamento por area logica.

Limitacoes reais:

- graficos nao sao renderizados;
- formulas visuais e merges complexos podem aparecer degradados;
- estilos muito especificos de planilha nao sao completos.

### PPTX

Abordagem:

- leitura OOXML via `jszip`
- parse de slides, shapes e imagens
- preservacao de blocos textuais e posicionamento basico

Preserva bem:

- slide de origem;
- titulo;
- bullets;
- blocos de texto;
- imagens relevantes;
- layout basico por coordenadas.

Limitacoes reais:

- sem animacoes;
- sem transicoes;
- sem fidelidade total a temas e masters complexos.

### HTML

Abordagem:

- parse nativo do DOM
- normalizacao estrutural

Preserva bem:

- estrutura semantica;
- listas;
- tabelas;
- links;
- imagens.

Limitacao real:

- scripts e comportamentos dinamicos nao fazem parte do viewer estrutural.

### TXT

Abordagem:

- parse direto com preservacao de quebras e espacos relevantes

Preserva bem:

- quebras de linha;
- blocos de texto;
- estrutura basica por paragrafos.

Limitacao real:

- nao existe hierarquia visual rica nativa; a fidelidade se limita ao texto literal.

### Imagens

Abordagem:

- preservacao da imagem original como asset visual

Preserva bem:

- fidelidade visual da imagem;
- referencia exata ao arquivo original.

Limitacao real:

- nao ha estrutura textual sem OCR separado.

## Regras de Fidelidade

O viewer foi desenhado para nao reinterpretar o conteudo do chunk.

Regras adotadas:

- nao reescrever o texto;
- nao normalizar o texto para melhorar leitura na camada de exibicao;
- nao gerar HTML artificial “bonito” quando o trecho original nao sustenta isso;
- usar fallback apenas quando o trecho nao puder ser projetado com rastreabilidade segura.

## Preparacao para RAG

Essa implementacao deixa o sistema pronto para evoluir para um fluxo mais forte de retrieval porque o mesmo chunk passa a ter:

- `chunkId` estavel;
- `contentHash` estavel;
- `normalizedText` para embeddings;
- `formattedContent` e `formattedHtml` para visualizacao fiel;
- `sourceMap` com pagina/secao/aba/slide/offset quando disponivel.

O proximo passo ideal e persistir no backend a versao formatada do chunk para eliminar dependencia do matching no cliente para todos os formatos.

## Testes e Validacao

Validado com:

- testes focados do detalhe documental;
- testes do motor de chunking estrutural;
- build completo do frontend.

Arquivos de teste adicionados/atualizados:

- `web/features/documents/components/DocumentInspectorDetailConsole.test.tsx`
- `web/features/documents/viewer/chunking.test.ts`

## Proximos Passos Recomendados

1. Persistir no backend a versao formatada do chunk e seu source map completo.
2. Adicionar parser estruturado no backend para DOCX/XLSX/PPTX/PDF e enviar AST pronta para o frontend.
3. Evoluir a navegacao por pagina/secao/slide para sincronizacao mais forte entre outline e destaque visual.
4. Adicionar assets e snapshots visuais para testes com arquivos reais por formato.
5. Integrar a mesma estrutura ao fluxo de indexacao para que retrieval e viewer compartilhem exatamente a mesma origem semantica e visual.