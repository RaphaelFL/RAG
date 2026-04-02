$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\stop-chatbot-api.ps1"

Push-Location $root
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'https://localhost:15213;http://localhost:15214'
    Remove-Item Env:ASPNETCORE_HTTP_PORTS -ErrorAction SilentlyContinue
    Remove-Item Env:ASPNETCORE_HTTPS_PORTS -ErrorAction SilentlyContinue

    dotnet run --project .\src\Api\Chatbot.Api.csproj
}
finally {
    Pop-Location
}