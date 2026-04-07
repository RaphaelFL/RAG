using Chatbot.Application.Contracts;
using FluentValidation;

namespace Chatbot.Api.Contracts;

public class ChatRequestValidator : AbstractValidator<ChatRequestDto>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
    }
}