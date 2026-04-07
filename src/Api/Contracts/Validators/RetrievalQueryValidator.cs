using Chatbot.Application.Contracts;
using FluentValidation;

namespace Chatbot.Api.Contracts;

public class RetrievalQueryValidator : AbstractValidator<RetrievalQueryDto>
{
    public RetrievalQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.TopK).InclusiveBetween(1, 50);
    }
}