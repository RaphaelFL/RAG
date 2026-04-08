using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedPythonCodePolicy
{
    private static readonly string[] BlockedPatterns =
    [
        "import os",
        "from os",
        "import subprocess",
        "from subprocess",
        "import socket",
        "from socket",
        "import requests",
        "from requests",
        "import urllib",
        "from urllib",
        "import http",
        "from http",
        "import ctypes",
        "from ctypes",
        "import shutil",
        "from shutil",
        "eval(",
        "exec(",
        "compile(",
        "__import__(",
        "os.system",
        "subprocess.",
        "socket.",
        "Path('..",
        "Path(\"..",
        "../",
        "..\\"
    ];

    public bool TryValidate(CodeInterpreterRequest request, CodeInterpreterOptions options, out string message)
    {
        if (!options.Enabled)
        {
            message = "Code interpreter desabilitado nesta configuracao.";
            return false;
        }

        if (!string.Equals(request.Language, "python", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(options.Runtime, "python", StringComparison.OrdinalIgnoreCase))
        {
            message = "Somente o runtime python esta habilitado nesta etapa.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            message = "Nenhum codigo foi informado para execucao.";
            return false;
        }

        if (request.Code.Length > 20_000)
        {
            message = "Codigo excede o limite maximo permitido para execucao controlada.";
            return false;
        }

        if (TryFindBlockedPattern(request.Code, out var blockedPattern))
        {
            message = $"Codigo bloqueado pela policy de seguranca: {blockedPattern}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static CodeInterpreterResult Disabled(string message)
    {
        return new CodeInterpreterResult
        {
            ExitCode = -1,
            StdErr = message,
            StdOut = string.Empty,
            OutputArtifacts = Array.Empty<string>()
        };
    }

    private static bool TryFindBlockedPattern(string code, out string blockedPattern)
    {
        var normalized = code.Replace("\r\n", "\n", StringComparison.Ordinal).ToLowerInvariant();
        blockedPattern = BlockedPatterns.FirstOrDefault(pattern => normalized.Contains(pattern.ToLowerInvariant(), StringComparison.Ordinal)) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(blockedPattern);
    }
}