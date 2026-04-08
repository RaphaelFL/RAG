param(
    [switch]$VersionOnly,
    [switch]$ForceLocalBuild,
    [string]$OllamaModel = "qwen2.5-coder:7b",
    [string]$OllamaBaseUrl = "http://localhost:11434/v1",
    [switch]$SkipOllamaCheck,
    [switch]$KeepKubeconfig
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$openClaudePath = Join-Path $root 'external\openclaude'

if (-not (Test-Path $openClaudePath)) {
    throw "Repositorio OpenClaude nao encontrado em $openClaudePath"
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js 20+ nao encontrado no PATH.'
}

function Use-OllamaDefaults {
    $env:CLAUDE_CODE_USE_OPENAI = '1'
    $env:OPENAI_BASE_URL = $OllamaBaseUrl
    $env:OPENAI_MODEL = $OllamaModel

    if (-not $SkipOllamaCheck) {
        $ollama = Get-Command ollama -ErrorAction SilentlyContinue
        if (-not $ollama) {
            throw "Ollama nao encontrado no PATH. Instale em https://ollama.com/download, depois rode: ollama pull $OllamaModel"
        }

        $availableModels = @(ollama list 2>$null)
        if (-not ($availableModels | Select-String -SimpleMatch $OllamaModel)) {
            throw "Modelo Ollama '$OllamaModel' nao encontrado. Rode: ollama pull $OllamaModel"
        }
    }
}

function Set-LocalOpenClaudeEnvironment {
    $env:CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = '0'

    if (Test-Path Env:ITERM_SESSION_ID) {
        Remove-Item Env:ITERM_SESSION_ID -ErrorAction SilentlyContinue
    }

    if ($env:TERM_PROGRAM -eq 'iTerm.app') {
        Remove-Item Env:TERM_PROGRAM -ErrorAction SilentlyContinue
    }

    if (-not $KeepKubeconfig -and (Test-Path Env:KUBECONFIG)) {
        Remove-Item Env:KUBECONFIG -ErrorAction SilentlyContinue
    }
}

if (-not (Get-Command rg -ErrorAction SilentlyContinue)) {
    Write-Warning 'ripgrep (rg) nao foi encontrado. Algumas funcoes do OpenClaude podem falhar.'
}

function Start-NpxOpenClaude {
    Use-OllamaDefaults
    Set-LocalOpenClaudeEnvironment

    if ($VersionOnly) {
        npx -y @gitlawb/openclaude --version
        return
    }

    npx -y @gitlawb/openclaude
}

$bun = Get-Command bun -ErrorAction SilentlyContinue
if (-not $bun) {
    if ($ForceLocalBuild) {
        throw 'Bun nao encontrado. O build local do OpenClaude exige Bun.'
    }

    Write-Warning 'Bun nao encontrado. Usando fallback via npx para executar o OpenClaude.'
    Start-NpxOpenClaude
    return
}

Push-Location $openClaudePath
try {
    Use-OllamaDefaults
    Set-LocalOpenClaudeEnvironment

    if (-not (Test-Path (Join-Path $openClaudePath 'node_modules'))) {
        bun install
    }

    if (-not (Test-Path (Join-Path $openClaudePath 'dist\cli.mjs'))) {
        bun run build
    }

    if ($VersionOnly) {
        node .\dist\cli.mjs --version
        return
    }

    node .\dist\cli.mjs
}
finally {
    Pop-Location
}