using System.Globalization;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Chatbot.Application.Services;

public sealed class DocumentMetadataSuggestionService : IDocumentMetadataSuggestionService
{
    private static readonly (string Category, string[] Keywords)[] CategoryRules =
    [
        ("arquitetura", ["arquitetura", "api", "apis", "integracao", "sistema", "servico", "servicos", "microservico", "infraestrutura"]),
        ("financeiro", ["financeiro", "reembolso", "pagamento", "orcamento", "despesa", "fiscal", "fatura", "nota", "contabil"]),
        ("juridico", ["juridico", "contrato", "clausula", "parecer", "compliance", "lgpd", "confidencialidade", "termo"]),
        ("seguranca", ["seguranca", "incidente", "acesso", "senha", "credencial", "vulnerabilidade", "risco"]),
        ("rh", ["rh", "ferias", "beneficio", "colaborador", "folha", "admissao", "admissional", "desligamento", "demissional", "treinamento", "aso", "exame"]),
        ("operacoes", ["operacao", "processo", "procedimento", "atendimento", "suporte", "sla", "execucao", "fluxo"]),
        ("politicas", ["politica", "politicas", "diretriz", "norma", "manual", "regulamento", "governanca", "padrao"])
    ];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "das", "dos", "e", "em", "para", "por", "com", "sem", "uma", "um", "as", "os", "na", "no",
        "documento", "titulo", "arquivo", "versao", "the", "and"
    };

    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly ILogger<DocumentMetadataSuggestionService> _logger;

    public DocumentMetadataSuggestionService(
        IDocumentTextExtractor documentTextExtractor,
        ILogger<DocumentMetadataSuggestionService> logger)
    {
        _documentTextExtractor = documentTextExtractor;
        _logger = logger;
    }

    public async Task<DocumentMetadataSuggestionDto> SuggestAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        DocumentTextExtractionResultDto extraction;
        try
        {
            extraction = await _documentTextExtractor.ExtractAsync(command, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsPdfDocument(command))
        {
            _logger.LogWarning(ex, "Falha ao extrair texto de PDF para sugestao de metadata. Aplicando fallback por nome do arquivo: {fileName}", command.FileName);
            extraction = new DocumentTextExtractionResultDto
            {
                Text = string.Empty,
                Strategy = "filename-fallback"
            };
        }

        var extractedText = NormalizeLineEndings(extraction.Text);
        var normalizedText = NormalizeWhitespace(extractedText);
        var suggestedTitle = SuggestTitle(command.FileName, extractedText);
        var suggestedCategory = SuggestCategory(command.FileName, normalizedText);
        var suggestedTags = SuggestTags(command.FileName, suggestedTitle, suggestedCategory, normalizedText);

        return new DocumentMetadataSuggestionDto
        {
            SuggestedTitle = suggestedTitle,
            SuggestedCategory = suggestedCategory,
            SuggestedCategories = string.IsNullOrWhiteSpace(suggestedCategory)
                ? new List<string>()
                : new List<string> { suggestedCategory },
            SuggestedTags = suggestedTags,
            Strategy = $"heuristic-{(string.IsNullOrWhiteSpace(extraction.Strategy) ? "direct" : extraction.Strategy)}",
            PreviewText = BuildPreview(normalizedText)
        };
    }

    private static string SuggestTitle(string fileName, string extractedText)
    {
        foreach (var line in NormalizeLineEndings(extractedText).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(8))
        {
            var normalizedLine = NormalizeWhitespace(line);
            if (!LooksLikeTitle(normalizedLine))
            {
                continue;
            }

            return NormalizeTitle(normalizedLine);
        }

        return HumanizeFileName(fileName);
    }

    private static string SuggestCategory(string fileName, string extractedText)
    {
        var corpus = Tokenize($"{Path.GetFileNameWithoutExtension(fileName)} {extractedText}");
        var bestMatch = CategoryRules
            .Select(rule => new
            {
                rule.Category,
                Score = corpus.Count(token => rule.Keywords.Contains(token, StringComparer.OrdinalIgnoreCase))
            })
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Category, StringComparer.Ordinal)
            .FirstOrDefault();

        return bestMatch is null || bestMatch.Score == 0 ? "geral" : bestMatch.Category;
    }

    private static List<string> SuggestTags(string fileName, string suggestedTitle, string? suggestedCategory, string extractedText)
    {
        var corpus = Tokenize($"{Path.GetFileNameWithoutExtension(fileName)} {suggestedTitle} {extractedText}");
        var tags = new List<string>();

        if (!string.IsNullOrWhiteSpace(suggestedCategory) && !string.Equals(suggestedCategory, "geral", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(suggestedCategory);
        }

        foreach (var (_, keywords) in CategoryRules)
        {
            foreach (var keyword in keywords)
            {
                if (corpus.Contains(keyword, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(keyword);
                }
            }
        }

        foreach (var token in Tokenize($"{suggestedTitle} {Path.GetFileNameWithoutExtension(fileName)}"))
        {
            if (token.Length <= 3 || StopWords.Contains(token))
            {
                continue;
            }

            tags.Add(token);
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static bool LooksLikeTitle(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length is < 4 or > 120)
        {
            return false;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length <= 14 && line.Count(char.IsLetter) >= 4;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim(' ', '-', '_', ':', '.', ';');
        var textInfo = CultureInfo.GetCultureInfo("pt-BR").TextInfo;

        return normalized.Equals(normalized.ToUpperInvariant(), StringComparison.Ordinal)
            ? textInfo.ToTitleCase(normalized.ToLowerInvariant())
            : normalized;
    }

    private static string HumanizeFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var builder = new StringBuilder(stem.Length);

        foreach (var character in stem)
        {
            builder.Append(character is '-' or '_' or '.' ? ' ' : character);
        }

        var normalized = NormalizeWhitespace(builder.ToString());
        var textInfo = CultureInfo.GetCultureInfo("pt-BR").TextInfo;
        return textInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static string BuildPreview(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return string.Empty;
        }

        return extractedText.Length <= 280
            ? extractedText
            : extractedText[..280].TrimEnd() + "...";
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeLineEndings(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static List<string> Tokenize(string value)
    {
        var normalized = RemoveDiacritics(value).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsPdfDocument(IngestDocumentCommand command)
    {
        return string.Equals(Path.GetExtension(command.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }
}