namespace Chatbot.Application.Services;

internal static class DocumentMetadataKeywordCatalog
{
    public static readonly (string Category, string[] Keywords)[] CategoryRules =
    [
        ("arquitetura", ["arquitetura", "api", "apis", "integracao", "sistema", "servico", "servicos", "microservico", "infraestrutura"]),
        ("financeiro", ["financeiro", "reembolso", "pagamento", "orcamento", "despesa", "fiscal", "fatura", "nota", "contabil"]),
        ("juridico", ["juridico", "contrato", "clausula", "parecer", "compliance", "lgpd", "confidencialidade", "termo"]),
        ("seguranca", ["seguranca", "incidente", "acesso", "senha", "credencial", "vulnerabilidade", "risco"]),
        ("rh", ["rh", "ferias", "beneficio", "colaborador", "folha", "admissao", "admissional", "desligamento", "demissional", "treinamento", "aso", "exame"]),
        ("operacoes", ["operacao", "processo", "procedimento", "atendimento", "suporte", "sla", "execucao", "fluxo"]),
        ("politicas", ["politica", "politicas", "diretriz", "norma", "manual", "regulamento", "governanca", "padrao"])
    ];

    public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "das", "dos", "e", "em", "para", "por", "com", "sem", "uma", "um", "as", "os", "na", "no",
        "documento", "titulo", "arquivo", "versao", "the", "and"
    };
}