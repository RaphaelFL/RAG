using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

internal static class ChatEvidenceExtensions
{
    public static bool ContainsAny(this string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
