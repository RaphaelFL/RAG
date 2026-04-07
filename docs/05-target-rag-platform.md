# Plataforma RAG Enterprise - Arquitetura Alvo

## Objetivo

Transformar a solucao atual em uma plataforma RAG modular, extensivel e pronta para producao, com geracao de embeddings dentro da propria solucao, persistencia vetorial real, retrieval governado, observabilidade fim a fim e camada agentic controlada.

## Diagnostico Atual

O estado atual do repositorio possui:

- API ASP.NET Core funcional com chat, busca, ingestao e SSE.
- Pipeline basica de ingestao com parsing direto, OCR configuravel, chunking e embeddings por provider.
- Busca hibrida local em disco e adapters opcionais para backends vetoriais externos quando fizer sentido.
- Cache de aplicacao, MCP basico, rate limiting, logs estruturados e tracing inicial.

As principais lacunas para o alvo enterprise sao:

- sem banco vetorial real como fluxo principal;
- sem parser dedicado para XLSX e PPTX;
- sem extracao estruturada robusta para tabelas e formularios;
- sem prompt assembler dedicado e token budgeting explicito como boundary;
- sem runtime agentic governado por contratos e budgets;
- sem Redis assumindo papel principal de coordenacao, locks e cache distribuido;
- sem camada de embeddings interna ao dominio da plataforma.

## Decisoes Arquiteturais

### 1. Core transacional em .NET

O nucleo permanece em ASP.NET Core .NET 8+ com Clean Architecture.

- Api: contratos HTTP, autenticacao, rate limits, tenancy e observabilidade.
- Application: orquestracao de casos de uso e politicas de negocio.
- Domain: entidades, value objects, contratos de consistencia.
- Infrastructure: adapters concretos para storage, Redis, vector store, model runtimes, MCP e ferramentas externas.

### 2. Embeddings internos como capacidade da plataforma

A geracao de embeddings deixa de ser tratada como chamada externa obrigatoria e passa a ser uma capacidade interna da plataforma atraves de um servico de aplicacao dedicado.

Escolha principal:

- `IEmbeddingGenerationService` como fronteira da aplicacao.
- `IEmbeddingModel` como contrato para modelos plugaveis.
- backend padrao recomendado: runtime local com ONNX Runtime para modelos sentence-transformer exportados.

Justificativa:

- menor acoplamento operacional do que depender de uma API de embeddings separada;
- melhor controle de versionamento de modelo e reindexacao seletiva;
- compatibilidade com execucao on-premises e politicas enterprise.

### 3. Banco vetorial principal

Escolha principal: PostgreSQL com pgvector como padrao de producao.

Motivos:

- stack operacional estavel e conhecida em ambientes enterprise;
- filtros por metadados e ACL no mesmo ecossistema relacional;
- menor complexidade de operacao que bancos vetoriais especializados no primeiro corte;
- suporte a busca vetorial, hibrida e top-k configuravel.

Abstracao obrigatoria:

- `IVectorStore`.

Implementacoes previstas:

- `PgVectorStore` como padrao;
- `ExternalVectorStoreAdapter` como compatibilidade futura;
- `LocalPersistentVectorStore` apenas para dev/local.

### 4. Redis no fluxo principal

Redis deixa de ser opcional decorativo e entra como componente operacional oficial para:

- cache de retrieval;
- cache de prompt assembly;
- cache de embeddings repetidos;
- locks distribuidos de ingestao/reindexacao;
- sessao curta de agentes;
- deduplicacao transitoria;
- coordenacao leve de filas.

### 5. Retrieval e prompt assembly como componentes separados

Separacao obrigatoria:

- `IRetriever`: dense, hybrid, metadata filtering, ACL filtering, top-k.
- `IReranker`: reranking opcional, thresholds, parent-child retrieval.
- `IPromptAssembler`: montagem do prompt final, token budgeting, compressao e deduplicacao.

Essa separacao evita um servico generico gigante e facilita evolucao por etapas.

### 6. Agentes com governanca

O fluxo agentic nao sera o nucleo transacional da API. O core continua deterministicamente orientado a retrieval e prompt assembly.

Agentes previstos:

- `OrchestratorAgent`
- `RetrievalAgent`
- `FileSearchAgent`
- `WebSearchAgent`
- `CodeInterpreterAgent`
- `StructuredExtractionAgent`
- `PromptAssemblyAgent`
- `AnswerSynthesisAgent`

Governanca minima:

- tool budget por requisicao;
- timeout por agent;
- limite de profundidade e numero de hops;
- trilha de auditoria;
- politicas de fallback.

### 7. Papel dos frameworks

#### Semantic Kernel

Framework principal para integracao com modelos, plugins, filters, planning controlado e function calling.

#### AutoGen

Fica desacoplado do core transacional e sera usado apenas em workflows especializados de laboratorio ou automacao complexa.

#### LlamaIndex e LlamaHub

Nao entram no coracao da regra de negocio. Serao integrados via adapters para pipelines de ingestao, loaders e estrategias de retrieval quando agregarem valor.

#### MCP

Permanece como camada de interoperabilidade para tools, resources e prompts, sem substituir os contratos internos de dominio.

## Fluxos Alvo

## Fluxo de Ingestao

1. Upload autenticado.
2. Validacao de assinatura, content type, ACL e tenant.
3. Parser dedicado por tipo de arquivo.
4. Normalizacao e limpeza.
5. Extracao estruturada opcional.
6. Chunking inteligente.
7. Geracao interna de embeddings em lote.
8. Persistencia de documento, versao, chunks e vetores.
9. Atualizacao de caches, estatisticas e logs de ingestao.

## Fluxo de Retrieval

1. Entrada do usuario.
2. Validacao de politica e contexto.
3. Dense retrieval e hybrid retrieval.
4. Filtros por tenant, ACL, tags, tipo e data.
5. Parent-child retrieval quando aplicavel.
6. Reranking opcional.
7. Budgeting de contexto.
8. Prompt assembly.
9. Chamada final para a LLM.
10. Pos-processamento, citacoes, logs e metricas.

## Modelo de Chunk

Campos obrigatorios por chunk:

- `chunk_id`
- `document_id`
- `document_version_id`
- `chunk_index`
- `content_hash`
- `parent_document_hash`
- `tenant_id`
- `source_type`
- `source_name`
- `page_number`
- `worksheet_name`
- `slide_number`
- `section_title`
- `table_id`
- `form_id`
- `tags`
- `acl`
- `text`
- `normalized_text`
- `summary`
- `entities`
- `embedding_model_name`
- `embedding_model_version`
- `vector_dimensions`
- `created_at`
- `updated_at`

Regras:

- hash nao participa do texto enviado ao embedding;
- identificadores opacos nao participam do payload semantico;
- o texto do embedding contem apenas conteudo semanticamente util;
- metadata tecnica e tratada separadamente da carga semantica.

## Estrutura Alvo de Pastas

```text
src/
  Api/
  Application/
    Abstractions/
    Contracts/
    Agents/
    Ingestion/
    Retrieval/
    Prompting/
  Domain/
    Entities/
    ValueObjects/
    Policies/
  Infrastructure/
    Embeddings/
    VectorStores/
    Redis/
    Mcp/
    WebSearch/
    CodeInterpreter/
    StructuredExtraction/
    Parsers/
  Workers/
    Embeddings/
    Ingestion/
```

## Endpoints Alvo

- `POST /api/v1/ingestion/jobs`
- `GET /api/v1/ingestion/jobs/{jobId}`
- `POST /api/v1/documents`
- `GET /api/v1/documents/{documentId}`
- `POST /api/v1/documents/{documentId}/reindex`
- `POST /api/v1/retrieval/query`
- `POST /api/v1/chat/completions`
- `POST /api/v1/chat/stream`
- `GET /api/v1/runtime/rag`
- `POST /api/v1/runtime/rag/reload`
- `GET /api/v1/agents/runs/{runId}`
- `POST /mcp`

## Seguranca

Controles obrigatorios:

- autenticacao e autorizacao forte;
- ACL por documento e chunk;
- isolamento por tenant;
- protecao contra prompt injection e tool injection;
- SSRF hardening para web search;
- sandbox para code interpreter;
- validacao de assinatura de arquivo;
- quotas e rate limiting;
- auditoria de uploads, retrieval, prompts, tools e respostas.

## Observabilidade

Metricas e trilhas obrigatorias:

- ingestao por etapa;
- latencia de embedding;
- cache hit e miss;
- retrieval dense/hybrid/rerank;
- prompt assembly budgeting;
- custo e tokens por chamada;
- documentos reindexados;
- fallback de providers;
- execucao de agentes e tools.

## Estrategia de Compatibilidade

O sistema atual nao sera descartado. A migracao sera incremental:

- manter controllers atuais como camada de compatibilidade;
- introduzir novos contratos e adapters lado a lado;
- mover a pipeline de ingestao e retrieval para boundaries explicitos;
- substituir gradualmente implementacoes locais simplificadas por componentes de producao.