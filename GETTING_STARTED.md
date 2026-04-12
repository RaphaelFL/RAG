# Como começar

## 1. Pré-requisitos locais

- .NET 8 SDK
- Node.js 20+
- Ollama com os modelos `qwen2.5-coder:7b`, `nomic-embed-text` e `llava`
- SQL Server local ou `docker compose`
- Redis local ou `docker compose`

Baixe os modelos do Ollama antes do primeiro uso:

```bash
ollama pull qwen2.5-coder:7b
ollama pull nomic-embed-text
ollama pull llava
```

## 2. Backend local

```bash
dotnet restore ChatbotApi.slnx
.\scripts\run-api.ps1
```

Health checks:

```bash
curl http://localhost:15214/health
curl http://localhost:15214/api/v1/health
```

Swagger:

```text
https://localhost:15213/swagger/index.html
```

## 3. Frontend local

```bash
cd web
npm install
npm run dev
```

UI:

```text
http://localhost:3000
```

## 4. Segredos locais

O backend carrega automaticamente estas sobreposicoes opcionais:

- `appsettings.local.json`
- `appsettings.{Environment}.local.json`
- `secrets.json`

Use [secrets.json](secrets.json) apenas na sua maquina. Para deploy, prefira variaveis de ambiente:

```text
ConnectionStrings__DefaultConnection
JWT__Key
JWT__SecKey
```

## 5. Headers obrigatórios

```text
Authorization: Bearer dev-token
X-Tenant-Id: 11111111-1111-1111-1111-111111111111
X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
X-User-Role: TenantAdmin
```

## 6. Exemplo mínimo de chat

```bash
curl -X POST http://localhost:15214/api/v1/chat/message \
  -H "Authorization: Bearer dev-token" \
  -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
  -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
  -H "X-User-Role: TenantAdmin" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":\"550e8400-e29b-41d4-a716-446655440000\",\"message\":\"Quais sao as regras de reembolso?\",\"templateId\":\"grounded_answer\",\"templateVersion\":\"1.0.0\"}"
```

## 7. Exemplo mínimo de upload

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

Se quiser restringir acesso ao documento:

```text
accessPolicy={"allowedRoles":["Analyst"]}
```

## 8. Docker local

```bash
docker compose up --build
```

O compose sobe API, SQL Server e Redis Stack. Mantenha o Ollama rodando fora do container no host local.

Portas do compose:

- API compativel com frontend local: `http://localhost:15214`
- API acesso direto alternativo: `http://localhost:5000`
- SQL Server: `localhost:1433`
- Redis: `localhost:6379`
- Redis Stack UI: `http://localhost:8001`

## 9. Testes

```bash
dotnet test ChatbotApi.slnx
cd web
npm test
npm run test:e2e
```

## 10. Troubleshooting

### Porta em uso

```powershell
Get-NetTCPConnection -LocalPort 15214 -ErrorAction SilentlyContinue
```

### Certificado HTTPS local no Windows

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

### Limpar cache NuGet

```bash
dotnet nuget locals all --clear
```

## 11. Leituras seguintes

- [README.md](README.md)
- [START_HERE.md](START_HERE.md)
- [docs](docs)
