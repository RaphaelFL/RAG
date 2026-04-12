# Comece por aqui

Se quiser apenas subir tudo local e testar a API, use esta ordem.

## 1. Prepare o Ollama

```bash
ollama pull qwen2.5-coder:7b
ollama pull nomic-embed-text
ollama pull llava
```

O baseline atual usa somente providers locais para chat, embeddings e OCR por vision model.

## 2. Suba a API

```bash
dotnet restore ChatbotApi.slnx
.\scripts\run-api.ps1
```

Valide:

```bash
curl http://localhost:15214/health
```

Swagger:

```text
https://localhost:15213/swagger/index.html
```

## 3. Suba o frontend

```bash
cd web
npm install
npm run dev
```

Abra:

```text
http://localhost:3000
```

## 4. Se preferir Docker

```bash
docker compose up --build
```

Esse caminho sobe API, SQL Server e Redis Stack. O Ollama continua no host local.
No compose atual, a API fica disponivel em `http://localhost:15214` para compatibilidade com o frontend local e tambem em `http://localhost:5000` para acesso direto ao container.

## 5. Headers mínimos

- Authorization: Bearer dev-token
- X-Tenant-Id: guid
- X-User-Id: guid
- X-User-Role: TenantUser, Analyst, TenantAdmin, PlatformAdmin ou McpClient

## 6. O que já está pronto

- chat grounded com resposta normal e SSE
- histórico de sessão
- upload, ingestão e reindexação
- accessPolicy documental
- OCR local para imagens com Ollama vision
- embeddings internos com runtime Python local
- vector store local via Redis Stack
- frontend administrativo e feed de auditoria operacional

## 7. O que olhar depois

- [README.md](README.md)
- [GETTING_STARTED.md](GETTING_STARTED.md)
- [EXTERNAL_AGENTS.md](EXTERNAL_AGENTS.md)
- [docs](docs)
