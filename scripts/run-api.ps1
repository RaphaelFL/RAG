$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\stop-chatbot-api.ps1"

Push-Location $root
try {
    dotnet run --project .\src\Api\Chatbot.Api.csproj
}
finally {
    Pop-Location
}