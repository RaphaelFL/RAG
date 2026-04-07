# Plano Incremental de Refatoracao para Plataforma RAG Enterprise

## Principios

- simplicidade operacional antes de extensibilidade sofisticada;
- migracao em fatias verticais, sem big bang;
- preservar compatibilidade dos endpoints atuais sempre que possivel;
- nao acoplar o core a frameworks externos.

## Fase 0 - Fundacao Arquitetural

Objetivo:

- criar contratos novos sem quebrar a solucao atual.

Entregas:

- novas entidades de dominio para documents, versions, chunks, embeddings, prompt assemblies e agent runs;
- novos contratos de aplicacao para ingestao, retrieval, prompt assembly e tools;
- documentacao da arquitetura alvo;
- opcoes estruturadas para vector store, embeddings, Redis, MCP e code interpreter.

Criterio de aceite:

- build continua verde;
- arquitetura alvo documentada e revisavel;
- contratos estaveis para as proximas fases.

## Fase 1 - Embeddings Internos e Versionamento

Objetivo:

- remover a dependencia arquitetural de API externa como caminho principal de embeddings.

Entregas:

- `IEmbeddingModel` e `IEmbeddingGenerationService` implementados;
- backend local com modelo ONNX interno;
- versionamento por `embedding_model_name` e `embedding_model_version`;
- deduplicacao por `content_hash`;
- reindexacao seletiva por versao de embedding.

Criterio de aceite:

- chunks gerados pela ingestao ja saem com vetores produzidos internamente pela plataforma;
- API externa fica apenas como fallback controlado e nao como caminho principal.

## Fase 2 - Persistencia Vetorial Real

Objetivo:

- substituir o indice JSON local por persistencia vetorial real.

Entregas:

- `PgVectorStore`;
- schema para documentos, versoes, chunks e embeddings;
- filtros por tenant, ACL, tags e datas;
- top-k, dense retrieval e hybrid retrieval.

Criterio de aceite:

- retrieval nao depende de arquivo JSON local para producao;
- consultas suportam filtros e score threshold.

## Fase 3 - Ingestao Especializada

Objetivo:

- adicionar parsers e extracao estruturada por tipo.

Entregas:

- parser dedicado para XLSX;
- parser dedicado para PPTX;
- melhoria do parser de PDF nativo;
- pipeline de PDF escaneado com OCR por pagina;
- extracao de tabelas e formularios para JSON estruturado;
- chunking por worksheet, slide, heading, tabela e hierarquia pai-filho.

Criterio de aceite:

- documentos XLSX e PPTX preservam estrutura semantica relevante;
- tabelas e formularios complexos sao indexados como estrutura e texto.

## Fase 4 - Prompt Assembly e Retrieval Governado

Objetivo:

- separar retrieval de prompt assembly e governar o budget da resposta.

Entregas:

- `IRetriever`, `IReranker` e `IPromptAssembler` concretos;
- compressao de contexto;
- deduplicacao de evidencias;
- agrupamento por documento;
- citacoes humanas e rastreaveis;
- logs de retrieval e prompt assembly.

Criterio de aceite:

- prompt final e montado por boundary dedicado antes da chamada a LLM;
- hashes e ids tecnicos nao entram no contexto semantico.

## Fase 5 - Redis e Coordenacao Distribuida

Objetivo:

- transformar Redis em componente principal de coordenacao.

Entregas:

- cache distribuido para retrieval, prompts e embeddings;
- distributed locks;
- deduplicacao transitoria;
- sessao curta agentic;
- invalidacao por versionamento de embeddings e documentos.

Criterio de aceite:

- cache deixa de ser apenas process-local;
- reindexacoes concorrentes nao colidem.

## Fase 6 - MCP, Web Search e Code Interpreter

Objetivo:

- padronizar interoperabilidade e tools enterprise.

Entregas:

- resources, tools e prompts MCP por adapters;
- web search com allowlist, cache, timeouts e citacoes;
- code interpreter sandboxed com auditoria;
- file search agentic sobre base interna.

Criterio de aceite:

- tools expostas com contratos claros, budget e trilha de auditoria.

## Fase 7 - Agentic Runtime Governado

Objetivo:

- adicionar agentes sem capturar o core transacional.

Entregas:

- runtime de agentes com Semantic Kernel no core;
- AutoGen desacoplado para workflows especializados;
- budgets, timeouts, depth limits e fallback policies;
- observabilidade por agent run e tool execution.

Criterio de aceite:

- nenhum fluxo entra em loop ou tool calling sem budget;
- rastreabilidade completa por requisicao.