# Índice rápido do repositório

## Arquivos raiz

| Arquivo | Uso principal |
|---------|---------------|
| `ChatbotApi.slnx` | solução .NET |
| `README.md` | visão geral e configuração |
| `GETTING_STARTED.md` | quick start operacional |
| `START_HERE.md` | ordem mínima para subir a stack |
| `SUMMARY.md` | resumo do estado atual |
| `Dockerfile` | build da API |
| `docker-compose.yml` | stack local com API, SQL Server e Redis |

## Backend

| Caminho | Conteúdo |
|---------|----------|
| `src/Api` | startup, controllers, middleware e appsettings |
| `src/Application` | contratos, serviços e orquestração |
| `src/Domain` | entidades e modelos de domínio |
| `src/Infrastructure` | persistência, providers e tooling local |
| `src/Ingestion` | parsing, OCR, chunking e ingestão |
| `src/Retrieval` | busca e recuperação |
| `src/Mcp` | endpoints MCP |

## Frontend

| Caminho | Conteúdo |
|---------|----------|
| `web/app` | rotas e layout Next.js |
| `web/features` | features de chat e administração |
| `web/lib` | cliente HTTP, utilitários e proxy |
| `web/e2e` | testes end-to-end |

## Testes

| Caminho | Conteúdo |
|---------|----------|
| `tests/Backend.Unit` | testes unitários do backend |
| `tests/Backend.Integration` | testes de integração |

## Documentação

| Caminho | Conteúdo |
|---------|----------|
| `docs/01-api-documentation.md` | endpoints e contratos |
| `docs/02-arquitetura-implementada.md` | arquitetura técnica |
| `docs/03-seguranca-implementada.md` | segurança e políticas |
| `docs/04-proximas-etapas.md` | roadmap técnico |
| `docs/05-target-rag-platform.md` | arquitetura alvo |
| `docs/06-incremental-refactor-plan.md` | plano incremental |

## Arquivos úteis para operação local

| Arquivo | Função |
|---------|--------|
| `scripts/run-api.ps1` | sobe a API local |
| `scripts/stop-chatbot-api.ps1` | encerra a API local |
| `scripts/run-agent-stack.ps1` | sobe stack com OpenClaude |
| `scripts/run-openclaude.ps1` | sobe OpenClaude usando Ollama |
| `src/Api/appsettings.json` | baseline local da API |
| `secrets.json` | segredos locais gitignored |
