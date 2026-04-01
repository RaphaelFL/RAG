$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\stop-chatbot-api.ps1"

Push-Location $root
try {
    dotnet build .\ChatbotApi.slnx
}
finally {
    Pop-Location
}