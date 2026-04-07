using Chatbot.Application.Contracts;
using FluentValidation;

namespace Chatbot.Api.Contracts;

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
