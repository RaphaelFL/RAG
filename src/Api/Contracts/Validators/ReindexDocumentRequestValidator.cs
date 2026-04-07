using Chatbot.Application.Contracts;
using FluentValidation;

namespace Chatbot.Api.Contracts;

public class ReindexDocumentRequestValidator : AbstractValidator<ReindexDocumentRequestDto>
{
    public ReindexDocumentRequestValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}
