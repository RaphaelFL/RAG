# Resumo da solução

Estado atual do workspace em modo local-first.

## Stack ativa

- backend ASP.NET Core .NET 8
- frontend Next.js 15 + React 19
- chat e vision via Ollama local
- embeddings internos via runtime Python local
- Redis Stack como vector store e coordenação local
- persistência documental local em `artifacts/local-rag`
- auditoria operacional com feed paginado na UI administrativa

## O que está pronto

- chat grounded com endpoint normal e SSE
- histórico de sessão
- upload, ingestão e reindexação
- extração direta para PDF, DOCX, XLSX e PPTX
- OCR local para imagens com modelo vision
- feed de auditoria operacional no backend e frontend
- endpoints MCP e runtime agentic controlado
- testes unitários e cobertura para partes críticas do runtime atual

## O que a stack local precisa

- Ollama no host local em `http://localhost:11434/v1`
- SQL Server local ou via `docker compose`
- Redis local ou via `docker compose`

## Portas principais

- frontend: `http://localhost:3000`
- backend HTTP: `http://localhost:15214`
- backend HTTPS: `https://localhost:15213`
- Swagger: `https://localhost:15213/swagger/index.html`
- Ollama: `http://localhost:11434/v1`

## Docker local

O compose atual sobe:

- API compativel com frontend local em `http://localhost:15214`
- API acesso direto alternativo em `http://localhost:5000`
- SQL Server em `localhost:1433`
- Redis Stack em `localhost:6379`
- Redis Stack UI em `http://localhost:8001`

Observação: o Ollama continua fora do compose, rodando no host local.

## Áreas principais do repositório

- `src/Api`: startup, controllers e configuração base
- `src/Application`: contratos e serviços de orquestração
- `src/Infrastructure`: providers, persistência e tooling local
- `src/Ingestion`: parsing, OCR e chunking
- `src/Retrieval`: busca e recuperação
- `web`: interface Next.js
- `tests`: testes backend e frontend

## Leituras úteis

- [README.md](README.md)
- [GETTING_STARTED.md](GETTING_STARTED.md)
- [START_HERE.md](START_HERE.md)
- [docs](docs)
