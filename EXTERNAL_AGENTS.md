# Agentes Externos Locais

Este repositório inclui a base `external/openclaude` para rodar o OpenClaude lado a lado com o backend.

## Estrutura local adotada

```text
external/
  openclaude/
scripts/
  run-api.ps1
  run-openclaude.ps1
  run-agent-stack.ps1
```

## OpenClaude

Uso principal neste workspace:

- CLI agentic multi-provider
- teste rápido com APIs compatíveis
- execução local quando o ambiente estiver configurado

O script `scripts/run-openclaude.ps1` agora sobe o OpenClaude em modo local por padrão, apontando para o Ollama via API OpenAI-compatible em `http://localhost:11434/v1`.

Modelo default adotado neste workspace:

- `qwen2.5-coder:7b` para chat/coding no OpenClaude

O script tenta usar o código local do repositório se `bun` estiver instalado. Se `bun` não existir na máquina, ele faz fallback para `npx @gitlawb/openclaude`.

No estado atual desta máquina, o fallback via `npx` funciona, mas `rg` ainda nao esta no `PATH`. O CLI abre, porem recursos de busca local podem ficar limitados ate esse binario ser instalado.

## Pré-requisitos locais

- .NET 8 SDK
- Node.js 20+
- npm 10+
- Ollama
- ripgrep (`rg`) para busca completa no OpenClaude
- opcional: `bun` para build local do OpenClaude

Antes do primeiro uso:

```powershell
ollama pull qwen2.5-coder:7b
ollama pull llava
```

## Como subir junto do backend

### Tudo em janelas separadas

```powershell
.\scripts\run-agent-stack.ps1
```

## Comandos individuais

### Backend

```powershell
.\scripts\run-api.ps1
```

### OpenClaude

```powershell
.\scripts\run-openclaude.ps1
```

Se quiser trocar o modelo:

```powershell
.\scripts\run-openclaude.ps1 -OllamaModel llama3.1:8b
```

Validação mínima:

```powershell
.\scripts\run-openclaude.ps1 -VersionOnly
```