param(
    [switch]$SkipBackend
)

$ErrorActionPreference = 'Stop'

$pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue)?.Source
if (-not $pwsh) {
    throw 'pwsh nao encontrado no PATH.'
}

$started = New-Object System.Collections.Generic.List[string]

if (-not $SkipBackend) {
    Start-Process -FilePath $pwsh -ArgumentList @('-NoExit', '-File', (Join-Path $PSScriptRoot 'run-api.ps1')) | Out-Null
    $started.Add('backend') | Out-Null
}

if ($started.Count -eq 0) {
    Write-Warning 'Nenhum processo foi iniciado. Remova algum -Skip* para subir a stack.'
    return
}

Write-Host ("Processos iniciados em janelas separadas: {0}" -f ($started -join ', '))
Write-Host 'O backend iniciado por run-api.ps1 concentra o runtime local e as responsabilidades agenticas da aplicacao.'