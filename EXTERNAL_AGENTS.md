# Runtime Local

O OpenClaude deixou de fazer parte do fluxo operacional deste workspace.

As responsabilidades locais de chat, orquestração e ferramentas agora ficam concentradas na API.

## Fluxo local atual

```text
frontend -> Chatbot.Api -> Ollama
```

## Comandos recomendados

### Backend

```powershell
.\scripts\run-api.ps1
```

### Atalho legado

```powershell
.\scripts\run-agent-stack.ps1
```

O atalho legado apenas abre a API local em uma janela separada.

## Observação

O script `scripts/run-openclaude.ps1` foi mantido apenas para compatibilidade e informa que o runtime externo foi descontinuado neste workspace.