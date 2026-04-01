$ErrorActionPreference = 'Stop'

function Get-ChatbotApiProcessIds {
    $processes = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
    $processIds = [System.Collections.Generic.HashSet[int]]::new()
    $pendingIds = [System.Collections.Generic.Queue[int]]::new()

    foreach ($process in $processes) {
        $isChatbotExecutable = $process.Name -eq 'Chatbot.Api.exe'
        $isChatbotDotnetHost = $process.Name -eq 'dotnet.exe' -and (
            $process.CommandLine -like '*Chatbot.Api*' -or
            $process.CommandLine -like '*src\\Api*'
        )

        if (-not ($isChatbotExecutable -or $isChatbotDotnetHost)) {
            continue
        }

        if ($processIds.Add([int]$process.ProcessId)) {
            $pendingIds.Enqueue([int]$process.ProcessId)
        }
    }

    while ($pendingIds.Count -gt 0) {
        $parentId = $pendingIds.Dequeue()

        foreach ($childProcess in $processes) {
            if ([int]$childProcess.ParentProcessId -ne $parentId) {
                continue
            }

            if ($processIds.Add([int]$childProcess.ProcessId)) {
                $pendingIds.Enqueue([int]$childProcess.ProcessId)
            }
        }
    }

    return @($processIds | Sort-Object -Descending)
}

$processIds = @(Get-ChatbotApiProcessIds)

if ($processIds.Count -eq 0) {
    Write-Host 'Nenhum processo da Chatbot.Api estava em execucao.'
    exit 0
}

foreach ($processId in $processIds) {
    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
}

$remainingIds = @()

for ($attempt = 0; $attempt -lt 30; $attempt++) {
    Start-Sleep -Milliseconds 500
    $remainingIds = @(Get-ChatbotApiProcessIds)

    if ($remainingIds.Count -eq 0) {
        Write-Host ("Processos encerrados: {0}" -f (($processIds | Sort-Object) -join ', '))
        exit 0
    }
}

throw ("Nao foi possivel encerrar todos os processos da Chatbot.Api. Ainda ativos: {0}" -f ($remainingIds -join ', '))