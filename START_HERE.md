# Comece por aqui

Se você quer subir o projeto sem perder tempo, siga esta ordem.

## 1. Backend local sem Azure para prompts

Antes de subir a API, instale o Ollama e baixe os modelos locais:

```bash
ollama pull qwen2.5-coder:7b
ollama pull nomic-embed-text
ollama pull llava
```

O appsettings local agora prioriza Ollama para chat e embeddings, usa OCR local por modelo vision e persiste blobs, catálogo e índice em disco dentro de artifacts/local-rag para nao depender da Azure no fluxo de prompts.

## 2. Backend

Na raiz do repositório:

```bash
dotnet restore ChatbotApi.slnx
.\scripts\run-api.ps1
```

Teste a saúde:

```bash
curl http://localhost:15214/health
curl http://localhost:15214/api/v1/health
```

## 3. Frontend

Em outro terminal:

```bash
cd web
npm install
npm run dev
```

Abra http://localhost:3000.

## 3.1 Agentes externos locais

Para abrir OpenClaude junto do backend em janelas separadas:

```bash
.\scripts\run-agent-stack.ps1
```

Detalhes de setup e validacao em [EXTERNAL_AGENTS.md](EXTERNAL_AGENTS.md).

## 4. Headers obrigatórios para usar a API local

Use sempre:

- Authorization: Bearer dev-token
- X-Tenant-Id: guid
- X-User-Id: guid
- X-User-Role: TenantUser, Analyst, TenantAdmin, PlatformAdmin ou McpClient

## 5. Exemplo mínimo de chat

```bash
curl -X POST http://localhost:15214/api/v1/chat/message \
   -H "Authorization: Bearer dev-token" \
   -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
   -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
   -H "X-User-Role: TenantAdmin" \
   -H "Content-Type: application/json" \
   -d "{\"sessionId\":\"550e8400-e29b-41d4-a716-446655440000\",\"message\":\"Quais sao as regras de reembolso?\",\"templateId\":\"grounded_answer\",\"templateVersion\":\"1.0.0\"}"
```

## 6. Exemplo mínimo de upload

```bash
curl -X POST http://localhost:15214/api/v1/documents/ingest \
   -H "Authorization: Bearer dev-token" \
   -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
   -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
   -H "X-User-Role: Analyst" \
   -F "file=@C:/temp/manual.txt" \
   -F "documentTitle=Manual Operacional" \
   -F "categories=financeiro"
```

Se quiser restringir o documento por papel/usuário:

```text
accessPolicy={"allowedRoles":["Analyst"],"allowedUserIds":["aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]}
```

## 7. Rodar testes

Backend:

```bash
dotnet test ChatbotApi.slnx
```

Frontend:

```bash
cd web
npm test
npm run test:e2e
```

## 8. Onde olhar depois

- [README.md](README.md): visão completa, configuração, backlog e comandos
- [GETTING_STARTED.md](GETTING_STARTED.md): exemplos rápidos de uso
- [docs](docs): documentação por área

## 9. O que já está implementado

- chat grounded com SSE e citations
- histórico de sessão
- upload, polling e reindexação
- accessPolicy documental
- MCP com auth e feature flag
- frontend com sanitização, papel e E2E

## 10. O que ainda nao usa provider local real

- Azure AI Search
- Azure Blob Storage
- Azure Document Intelligence
- camada agentic do baseline

Observacao: no modo local atual, imagens usam OCR via modelo vision do Ollama. PDF com texto embutido entra por extração direta. PDF escaneado sem texto embutido ainda nao tem renderização local de páginas para OCR completo.
