# Comece por aqui

Se você quer subir o projeto sem perder tempo, siga esta ordem.

## 1. Backend

Na raiz do repositório:

```bash
dotnet restore ChatbotApi.slnx
dotnet run --project src/Api
```

Teste a saúde:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/api/v1/health
```

## 2. Frontend

Em outro terminal:

```bash
cd web
npm install
npm run dev
```

Abra http://localhost:3000.

## 3. Headers obrigatórios para usar a API local

Use sempre:

- Authorization: Bearer dev-token
- X-Tenant-Id: guid
- X-User-Id: guid
- X-User-Role: TenantUser, Analyst, TenantAdmin, PlatformAdmin ou McpClient

## 4. Exemplo mínimo de chat

```bash
curl -X POST http://localhost:5000/api/v1/chat/message \
   -H "Authorization: Bearer dev-token" \
   -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
   -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
   -H "X-User-Role: TenantAdmin" \
   -H "Content-Type: application/json" \
   -d "{\"sessionId\":\"550e8400-e29b-41d4-a716-446655440000\",\"message\":\"Quais sao as regras de reembolso?\",\"templateId\":\"grounded_answer\",\"templateVersion\":\"1.0.0\"}"
```

## 5. Exemplo mínimo de upload

```bash
curl -X POST http://localhost:5000/api/v1/documents/ingest \
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

## 6. Rodar testes

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

## 7. Onde olhar depois

- [README.md](README.md): visão completa, configuração, backlog e comandos
- [GETTING_STARTED.md](GETTING_STARTED.md): exemplos rápidos de uso
- [docs](docs): documentação por área

## 8. O que já está implementado

- chat grounded com SSE e citations
- histórico de sessão
- upload, polling e reindexação
- accessPolicy documental
- MCP com auth e feature flag
- frontend com sanitização, papel e E2E

## 9. O que ainda não é provider real

- Azure OpenAI
- Azure AI Search
- Azure Blob Storage
- Azure Document Intelligence
- camada agentic do baseline
