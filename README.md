# RAG Corporativo

Sistema com backend ASP.NET Core .NET 8 e frontend Next.js 15 para chat grounded, ingestão documental, citations auditáveis, SSE, MCP e isolamento por tenant.

## Estado atual

O projeto já implementa o fluxo local de MVP pedido pelos arquivos .ia:

- backend com camadas Api, Application, Domain, Infrastructure, Ingestion, Retrieval e Mcp;
- frontend em Next.js 15 + React 19 + TypeScript;
- autenticação local por bearer/header para desenvolvimento;
- chat não streaming e streaming SSE;
- upload, polling de status e reindexação;
- prompt templates versionados;
- prompt injection detection básica;
- OCR com provider real por configuração;
- jobs em background com worker hospedado;
- OpenTelemetry, Serilog e Polly;
- testes unitários, integração, componente e E2E.

O que ainda é backlog estrutural:

- camada agentic real do baseline com Microsoft Agent Framework;
- autenticação corporativa real;
- contratos congelados por snapshot/schema em maior profundidade.

## Estrutura

```text
/src
  /Api
  /Application
  /Domain
  /Infrastructure
  /Ingestion
  /Retrieval
  /Mcp
/tests
  /Backend.Unit
  /Backend.Integration
/web
  /app
  /features
  /lib
  /types
```

## Pré-requisitos

- .NET 8 SDK
- Node.js 20+
- npm 10+
- Docker opcional

## Como rodar

### 1. Restaurar backend

```bash
dotnet restore ChatbotApi.slnx
```

### 2. Subir a API localmente

```bash
.\scripts\run-api.ps1
```

No ambiente local, o script sobe a API em https://localhost:15213 e http://localhost:15214.

### 3. Subir o frontend

```bash
cd web
npm install
npm run dev
```

O frontend sobe na URL definida pelo runtime do Next.js no ambiente local.

### 4. Rodar com Docker

```bash
docker compose up --build
```

O compose atual sobe apenas a API em container Linux, publicada em http://localhost:5000.

## Autenticação local de desenvolvimento

Os endpoints protegidos exigem:

- Authorization: Bearer qualquer-token-nao-vazio
- X-Tenant-Id: guid do tenant
- X-User-Id: guid do usuário
- X-User-Role: TenantUser, Analyst, TenantAdmin, PlatformAdmin ou McpClient

Exemplo de chamada simples com valores sanitizados:

```bash
curl -X POST https://api.exemplo.local/api/v1/chat/message \
  -H "Authorization: Bearer <token-de-desenvolvimento>" \
  -H "X-Tenant-Id: <tenant-id>" \
  -H "X-User-Id: <user-id>" \
  -H "X-User-Role: TenantAdmin" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":\"<session-id>\",\"message\":\"Quais sao as regras de reembolso?\",\"templateId\":\"grounded_answer\",\"templateVersion\":\"1.0.0\"}"
```

## Endpoints principais

### Chat

- POST /api/v1/chat/message
- POST /api/v1/chat/stream
- GET /api/v1/chat/sessions/{sessionId}

### Busca

- POST /api/v1/search/retrieve
- POST /api/v1/search/query

### Documentos

- POST /api/v1/documents/ingest
- POST /api/v1/documents/upload
- GET /api/v1/documents/{documentId}
- POST /api/v1/documents/{documentId}/reindex
- POST /api/v1/documents/reindex

### Saúde

- GET /health
- GET /api/v1/health

### MCP

- POST /mcp

## Access policy documental

O campo accessPolicy já é aplicado na autorização documental. O formato aceito hoje é JSON simples, por exemplo:

```json
{
  "allowedRoles": ["Analyst"],
  "allowedUserIds": ["<user-id>"],
  "allowPlatformAdminCrossTenant": false
}
```

Sem accessPolicy, o acesso segue a regra padrão do tenant atual. PlatformAdmin só cruza tenant quando a policy explicita allowPlatformAdminCrossTenant=true.

## Configuração

O backend agora usa esta convenção:

- configuração não sensível fica em [src/Api/appsettings.json](src/Api/appsettings.json)
- o único `appsettings` versionado usado como base no backend local é [src/Api/appsettings.json](src/Api/appsettings.json)
- o backend não usa arquivo `.env`
- credenciais e sobreposições locais opcionais devem ficar fora do versionamento

### Frontend

O frontend usa arquivo de ambiente próprio dentro de [web](web):

- use [web/.env](web/.env)
- apenas variáveis públicas `NEXT_PUBLIC_*` devem ir para o frontend
- o arquivo versionado deve conter apenas valores sanitizados e não sensíveis

Campos hoje suportados no frontend:

- `NEXT_PUBLIC_API_BASE_URL`: URL base pública da API. Exemplo sanitizado: `https://api.exemplo.local`
- `NEXT_PUBLIC_DEFAULT_TENANT_ID`: tenant padrão da UI para desenvolvimento
- `NEXT_PUBLIC_DEFAULT_USER_ID`: usuário padrão da UI para desenvolvimento
- `NEXT_PUBLIC_DEFAULT_USER_ROLE`: papel padrão da UI para desenvolvimento
- `NEXT_PUBLIC_DEFAULT_TEMPLATE_ID`: template padrão inicial do console
- `NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION`: versão padrão inicial do template
- `NEXT_PUBLIC_DEFAULT_USE_STREAMING`: liga ou desliga o checkbox inicial de SSE
- `NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE`: liga ou desliga o fallback de conhecimento geral na UI
- `NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS`: lista separada por vírgula com origens públicas liberadas no `connect-src` do CSP

O token não foi movido para `NEXT_PUBLIC_*` porque isso o expõe deliberadamente ao bundle do navegador. Ele continua sendo fornecido pela própria UI em runtime, começa vazio e não é persistido em storage.

As principais seções estão em [src/Api/appsettings.json](src/Api/appsettings.json):

- ChatModelOptions
- EmbeddingOptions
- SearchOptions
- BlobStorageOptions
- OcrOptions
- PromptTemplateOptions
- FeatureFlagOptions
- ExternalProviderClientOptions

### O que fica no appsettings

- `ChatModelOptions:Model`: nome lógico do modelo. Exemplo: `gpt-4.1`
- `ChatModelOptions:Temperature`, `MaxTokens`, `TopP`
- `EmbeddingOptions:Model`, `Dimensions`, `BatchSize`
- `SearchOptions:IndexName`: nome do índice. Exemplo: `chatbot-documents`
- `SearchOptions:SemanticConfigurationName`: nome da configuração semântica. Exemplo: `default`
- `BlobStorageOptions:ContainerName`: nome do container. Exemplo: `documents`
- `OcrOptions:PrimaryProvider`, `FallbackProvider`, `AzureDocumentIntelligenceModelId`
- `FeatureFlagOptions:*`
- `ExternalProviderClientOptions:TimeoutSeconds`
- `ExternalProviderClientOptions:*ApiVersion`

### Saneamento dos arquivos versionados

- `src/Api/appsettings.json` pode ser versionado desde que contenha apenas estrutura, placeholders ou valores operacionais nao sensiveis
- `web/.env` pode ser versionado se contiver apenas valores publicos e sanitizados
- segredos reais, chaves, tokens e credenciais nao devem aparecer nem no `README` nem em arquivos versionados
- `.env`, `.env.local`, `.env.development.local`, `.env.production.local`, `web/.env.local`, `web/.env.development.local`, `web/.env.production.local`, `appsettings.*.local.json` e `secrets.json` tambem foram deixados sanitizados para a primeira subida
- a API carrega automaticamente `appsettings.local.json`, `appsettings.{Environment}.local.json` e `secrets.json` opcionais no diretorio do projeto e nos diretorios-pai proximos, permitindo credenciais locais sem alterar `src/Api/appsettings.json`
- em desenvolvimento local, `scripts/run-api.ps1` fixa as portas esperadas pela UI; quando `ExternalProviderClientOptions:UseAzureAdAuthentication=true` estiver ativo em `src/Api/appsettings.json`, a primeira chamada real pode exigir login por device code e usar o timeout operacional maior previsto para autenticacao interativa

### Falha explícita de startup

A API agora falha no startup quando um provider real está parcialmente configurado ou quando a execução real é obrigatória e ainda faltam credenciais. Não existe mais fallback silencioso para chat mock, embeddings mock, OCR mock, blob em memória ou índice em memória fora de opt-in explícito para testes. Exemplos:

- Azure OpenAI com endpoint e key, mas sem deployment
- Azure AI Search com endpoint, mas sem key
- Document Intelligence com endpoint, mas sem key
- Blob Storage com connection string sem configuração mínima complementar

Isso evita subir a aplicação em um estado ambíguo.

Os typed clients Refit continuam registrados com timeout explícito para Azure OpenAI, Azure Search, Blob Storage e Google Vision. O runtime principal já usa providers reais para Azure OpenAI, Azure AI Search, Azure Blob Storage e Azure Document Intelligence quando a configuração necessária está presente.

## Testes

### Backend

```bash
dotnet test ChatbotApi.slnx
```

### Frontend unitário/componente

```bash
cd web
npm test
```

### Frontend E2E

```bash
cd web
npm run test:e2e
```

Cobertura validada atualmente inclui:

- grounded answer com citations;
- resposta explícita sem evidência suficiente;
- isolamento por tenant;
- accessPolicy por papel no documento;
- redaction de histórico quando a autorização atual não permite a fonte;
- upload inválido e malware hook;
- OCR direto e fallback;
- reindexação em background;
- SSE com started, delta, citation, completed e error;
- MCP ligado/desligado por feature flag;
- frontend com sanitização de Markdown, hydration, erros 401/403/429, streaming e E2E.

## Observabilidade

- Serilog em console e arquivo em src/Api/logs
- OpenTelemetry com tracing e metrics no runtime
- Correlation ID em X-Correlation-Id
- rate-limit headers nos endpoints protegidos

## Frontend

O frontend conversa apenas com a API HTTP. Não chama Azure diretamente. O token bearer digitado na UI não é persistido em storage do navegador. A UI já aplica bloqueio visual por papel para upload e reindexação.

## Limitações conhecidas

- sem `.env` preenchido, partes da stack externa continuam em fallback mock/in-memory;
- GraphRAG permanece desligado no MVP;
- a camada agentic do baseline ainda não foi materializada.

## Arquivos úteis

- [README.md](README.md)
- [START_HERE.md](START_HERE.md)
- [GETTING_STARTED.md](GETTING_STARTED.md)
- [docs](docs)
- [web](web)
- [tests](tests)
