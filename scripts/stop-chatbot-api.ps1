param(
    [int[]]$ExcludedProcessIds = @()
)

$ErrorActionPreference = 'Stop'

function Get-ChatbotApiProcessIds {
    $processes = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
    $processIds = [System.Collections.Generic.HashSet[int]]::new()
    $pendingIds = [System.Collections.Generic.Queue[int]]::new()
    $excludedIds = [System.Collections.Generic.HashSet[int]]::new()

    foreach ($excludedProcessId in $ExcludedProcessIds) {
        [void]$excludedIds.Add([int]$excludedProcessId)
    }

    $currentProcess = $processes | Where-Object { [int]$_.ProcessId -eq $PID } | Select-Object -First 1
    if ($null -ne $currentProcess) {
        [void]$excludedIds.Add([int]$currentProcess.ProcessId)
        [void]$excludedIds.Add([int]$currentProcess.ParentProcessId)
    }

    foreach ($process in $processes) {
        if ($excludedIds.Contains([int]$process.ProcessId)) {
            continue
        }

        $commandLine = [string]$process.CommandLine

        $isChatbotExecutable = $process.Name -eq 'Chatbot.Api.exe'
        $isChatbotDotnetHost = $process.Name -eq 'dotnet.exe' -and (
            (($commandLine -match '\brun\b') -and (
                $commandLine -like '*Chatbot.Api*' -or
                $commandLine -like '*src\\Api*'
            )) -or
            $commandLine -like '*Chatbot.Api.dll*'
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

            if ($excludedIds.Contains([int]$childProcess.ProcessId)) {
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