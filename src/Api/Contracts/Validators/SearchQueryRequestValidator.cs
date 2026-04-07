using Chatbot.Application.Contracts;
using FluentValidation;

namespace Chatbot.Api.Contracts;

public class SearchQueryRequestValidator : AbstractValidator<SearchQueryRequestDto>
{
    public SearchQueryRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Top).InclusiveBetween(1, 50);
    }
}