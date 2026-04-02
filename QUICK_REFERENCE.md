---
id: DOC-QUICK-REF
title: Quick Reference - Comandos e Endpoints
type: ops
status: approved
---

# ⚡ Quick Reference

## 🔧 Comandos Essenciais

### Build & Run

```bash
# Restaurar dependências
dotnet restore

# Compilar solução
dotnet build

# Executar API
.\scripts\run-api.ps1

# Executar com HTTPS
dotnet run --project src/Api/Chatbot.Api.csproj --urls "https://localhost:5001"

# Executar em porta diferente
dotnet run --project src/Api/Chatbot.Api.csproj --urls "http://localhost:5555"

# Limpar build
dotnet clean
```

### Docker

```bash
# Build e run com Docker Compose
docker compose up --build

# Parar containers
docker compose down

# Ver logs
docker compose logs -f chatbot-api
```

### Projeto Específico

```bash
# Build apenas a API
dotnet build src/Api

# Run tests
dotnet test

# Publish para produção
dotnet publish -c Release -o ./publish
```

## 🧪 Testes com cURL

### Chat - Resposta Completa

```bash
curl -X POST http://localhost:15214/api/v1/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "message": "O que é RAG?"
  }'
```

**Esperado**: 200 OK com `ChatResponseDto`

### Chat - Streaming (SSE)

```bash
curl -X POST http://localhost:15214/api/v1/chat/stream \
  -H "Content-Type: application/json" \
  -N \
  -d '{
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "message": "Explique RAG"
  }'
```

**Esperado**: Stream de eventos SSE

### Search - Retrieval

```bash
curl -X POST http://localhost:15214/api/v1/search/retrieve \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Política de reembolso",
    "topK": 5
  }'
```

**Esperado**: 200 OK com `RetrievalResultDto`

### Documents - Upload

```bash
curl -X POST http://localhost:15214/api/v1/documents/upload \
  -F "file=@documento.pdf" \
  -F "tags=financeiro,importante"
```

**Esperado**: 202 Accepted com `UploadDocumentResponseDto`

### Health Check

```bash
curl http://localhost:15214/health
```

**Esperado**: 
```json
{"status":"healthy","timestamp":"2026-03-31T22:00:00Z"}
```

## 📝 Headers Obrigatórios

```bash
# Exemplo com headers + body
curl -X POST http://localhost:15214/api/v1/chat/message \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
  -H "X-User-Role: TenantAdmin" \
  -H "X-Correlation-Id: abc-123-def" \
  -d '{"sessionId":"...","message":"..."}'
```

Resposta terá:
```
X-Correlation-Id: abc-123-def (echo)
```

## 📂 Pasta de Logs

```
logs/
├── chatbot-2026-03-31.txt
├── chatbot-2026-03-30.txt
└── ...
```

Ver logs:
```bash
# Windows
type logs/chatbot*.txt | more

# Linux/Mac
tail -f logs/chatbot*.txt
```

## 🏗️ Estrutura de Projeto

### Adicionar novo endpoint

1. Criar controller em `src/Api/Controllers/MeuController.cs`
2. Injetar serviço do Application
3. Adicionar DTO em `src/Api/Contracts/Dtos.cs`
4. Documentar em `docs/01-api-documentation.md`

### Adicionar novo serviço

1. Criar interface em `src/Application/Abstractions/ServiceInterfaces.cs`
2. Implementar em `src/Application/Services/CoreServices.cs`
3. Registrar em `src/Api/Program.cs`:
   ```csharp
   builder.Services.AddScoped<IMeuServico, MeuServico>();
   ```

### Adicionar nova entidade

1. Adicionar classe em `src/Domain/Entities/ChatEntities.cs`
2. Não referenciar Azure/Google específicos

## 🔍 Debugging

### Breakpoints in VS

1. Abrir `src/Api/Controllers/ChatController.cs`
2. Clicar na linha para adicionar breakpoint
3. Press F5
4. Fazer requisição via curl
5. Debugger pausa no breakpoint

### Visual Studio Code

1. Instalar C# DevKit
2. Launch.json será criado automaticamente
3. F5 para debug
4. F10 para step over, F11 para step into

### Logs

Adicionar logs em qualquer serviço:
```csharp
_logger.LogInformation("Mensagem: {param}", value);
_logger.LogWarning("Aviso");
_logger.LogError(ex, "Erro");
```

## ⚙️ Configuração

### Mudar porta

Padrão local atual:

```powershell
.\scripts\run-api.ps1
# HTTP:  http://localhost:15214
# HTTPS: https://localhost:15213
```

Ou via linha de comando:

```csharp
dotnet run --project src/Api/Chatbot.Api.csproj --urls "http://localhost:5555"
```

### Mudar logging level

`src/Api/appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Default": "Debug",  // Information, Warning, Error, Critical
    "Microsoft.AspNetCore": "Warning"
  }
}
```

### CORS - Mudar para produção

`src/Api/Program.cs`:
```csharp
// Dev (atual)
policy.AllowAnyOrigin()

// Prod (TODO)
policy.WithOrigins("https://seu-dominio.com")
```

## 🚨 Troubleshooting

### "Porta 15214 já em uso"

```bash
# Windows
netstat -ano | findstr :15214
taskkill /PID <pid> /F

# Linux/Mac
lsof -i :15214
kill -9 <pid>
```

Ou usar outra porta:
```bash
dotnet run --urls "http://localhost:5555"
```

### "Build failed - cannot find package"

```bash
# Limpar cache
dotnet nuget locals all --clear

# Restaurar
dotnet restore

# Build
dotnet build
```

### "SSL/TLS certificate error"

```bash
# Windows
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# Acessar via HTTP (não HTTPS)
curl http://localhost:15214/
```

### "Docker port already in use"

```bash
docker compose down
docker ps -a
docker rm <container_id>
docker compose up
```

## 📚 Documentação Rápida

| Você quer... | Vá para... |
|-------------|-----------|
| Executar API | `GETTING_STARTED.md` |
| Ver endpoints | `docs/01-api-documentation.md` |
| Entender arquitetura | `docs/02-arquitetura-implementada.md` |
| Próximos passos | `docs/04-proximas-etapas.md` |
| Procurar arquivo | `INDEX.md` |
| Overview visual | `SUMMARY.md` |
| Começar aqui | `START_HERE.md` |

## 🎯 Checklist Primeiro Dia

- [ ] Clone/acesse a pasta
- [ ] Run `dotnet restore`
- [ ] Run `dotnet build`
- [ ] Run `.\scripts\run-api.ps1`
- [ ] Teste `curl http://localhost:15214/health`
- [ ] Teste chat endpoint com curl
- [ ] Abra `docs/01-api-documentation.md`
- [ ] Explore estrutura em Visual Studio / VS Code

## 💡 Dicas Pro

### Performance
- Use HTTP em desenvolvimento (não HTTPS)
- Rode em Release mode para benchmark real
- Use Redis para cache quando implementado

### Debugging
- Adicione `_logger.LogInformation()` antes de linhas críticas
- Use `?` (nullable) para evitar null reference
- Breakpoints em middleware para ver requisição completa

### Produção
- Sempre ler `docs/04-proximas-etapas.md` antes de deploy
- Testar CORS, headers, autenticação antes de ir live
- Usar secrets management (Azure Key Vault)

---

**Last Updated**: 31.03.2026
**Version**: v0.1 MVP
