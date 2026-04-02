using Chatbot.Application.Contracts;
using FluentValidation;

namespace Chatbot.Api.Contracts;

/// <summary>
/// Validators para validação estrutural na camada API.
/// DTOs estão em Chatbot.Application.Contracts.
/// </summary>
public class ChatRequestValidator : AbstractValidator<ChatRequestDto>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
    }
}

public class RetrievalQueryValidator : AbstractValidator<RetrievalQueryDto>
{
    public RetrievalQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.TopK).InclusiveBetween(1, 50);
    }
}

public class ReindexDocumentRequestValidator : AbstractValidator<ReindexDocumentRequestDto>
{
    public ReindexDocumentRequestValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public class BulkReindexRequestValidator : AbstractValidator<BulkReindexRequestDto>
{
    public BulkReindexRequestValidator()
    {
        RuleFor(x => x)
            .Must(request => request.IncludeAllTenantDocuments || request.DocumentIds.Count > 0)
            .WithMessage("Informe documentIds ou marque IncludeAllTenantDocuments.");
        RuleForEach(x => x.DocumentIds).NotEmpty();
        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(mode => mode is "incremental" or "full")
            .WithMessage("Mode must be incremental or full.");
    }
}

public class SearchQueryRequestValidator : AbstractValidator<SearchQueryRequestDto>
{
    public SearchQueryRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Top).InclusiveBetween(1, 50);
    }
}
