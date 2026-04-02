# Como começar

## Backend

```bash
dotnet restore ChatbotApi.slnx
.\scripts\run-api.ps1
```

Saúde:

```bash
curl http://localhost:15214/health
curl http://localhost:15214/api/v1/health
```

## Frontend

```bash
cd web
npm install
npm run dev
```

## Headers locais obrigatórios

```text
Authorization: Bearer dev-token
X-Tenant-Id: 11111111-1111-1111-1111-111111111111
X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
X-User-Role: TenantAdmin
```

## Chat não streaming

```bash
curl -X POST http://localhost:15214/api/v1/chat/message \
  -H "Authorization: Bearer dev-token" \
  -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
  -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
  -H "X-User-Role: TenantAdmin" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":\"550e8400-e29b-41d4-a716-446655440000\",\"message\":\"Quais sao as regras de reembolso?\",\"templateId\":\"grounded_answer\",\"templateVersion\":\"1.0.0\"}"
```

## Chat streaming

```bash
curl -X POST http://localhost:15214/api/v1/chat/stream \
  -H "Authorization: Bearer dev-token" \
  -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
  -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
  -H "X-User-Role: TenantAdmin" \
  -H "Content-Type: application/json" \
  -N \
  -d "{\"sessionId\":\"550e8400-e29b-41d4-a716-446655440000\",\"message\":\"Explique a politica de reembolso\",\"templateId\":\"grounded_answer\",\"templateVersion\":\"1.0.0\"}"
```

## Upload com policy documental

```bash
curl -X POST http://localhost:15214/api/v1/documents/ingest \
  -H "Authorization: Bearer dev-token" \
  -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" \
  -H "X-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" \
  -H "X-User-Role: Analyst" \
  -F "file=@C:/temp/manual.txt" \
  -F "documentTitle=Manual Operacional" \
  -F "categories=financeiro" \
  -F "accessPolicy={\"allowedRoles\":[\"Analyst\"]}"
```

## Testes

```bash
dotnet test ChatbotApi.slnx
cd web
npm test
npm run test:e2e
```

## Docker

```bash
docker compose up --build
```

## Leituras seguintes

- [README.md](README.md)
- [START_HERE.md](START_HERE.md)
- [docs](docs)
{
  "code": "error_code",
  "message": "Human readable",
  "details": {
    "field": ["error message"]
  },
  "traceId": "00-abc123-def456-01"
}
```

## 10. Troubleshooting

### Porta em uso

```bash
# Encontrar processo usando a porta
lsof -i :15214

# Executar em porta diferente
dotnet run --urls "http://localhost:5002"
```

### Erro ao restaurar dependências

```bash
# Limpar cache NuGet
dotnet nuget locals all --clear

# Tentar novamente
dotnet restore
```

### SSL/TLS

```bash
# Se certificado inválido em Windows
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

## 11. Próximos Passos

1. ✅ Confirmar que a API executa
2. ⬜ Integrar com Azure AI Search (doc: 04-proximas-etapas.md)
3. ⬜ Integrar com Azure OpenAI
4. ⬜ Adicionar autenticação
5. ⬜ Implementar testes

## 12. Documentação Completa

Consulte:
- `docs/01-api-documentation.md` - Referência de endpoints
- `docs/02-arquitetura-implementada.md` - Arquitetura técnica
- `docs/03-seguranca-implementada.md` - Segurança
- `docs/04-proximas-etapas.md` - Roadmap
- `README.md` - Getting started

## 13. Apoio

Para debug:
1. Verifique `logs/` para mensagens de erro
2. Adicione breakpoints em `Program.cs` ou Controllers
3. Use DevTools do navegador para inspecionar requests/responses
4. Consulte comentários no código (próxima fase: melhorar documentação)

---

**Sucesso!** 🎉 A API base está pronta para integração com Azure.
