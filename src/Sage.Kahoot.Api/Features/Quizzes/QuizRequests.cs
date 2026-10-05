using FastEndpoints;
using FluentValidation;
using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Domain.Quizzes;

namespace Sage.Kahoot.Api.Features.Quizzes;

public sealed record OptionRequest(string Text, bool IsCorrect);

public sealed record QuestionRequest(
    string Text,
    int TimeLimitSeconds,
    int BasePoints,
    List<OptionRequest> Options);

public sealed record CreateQuizRequest(string Title, string? Description, List<QuestionRequest> Questions);

public sealed record UpdateQuizRequest(Guid QuizId, string Title, string? Description, List<QuestionRequest> Questions);

/// <summary>
/// Validação de forma de uma opção. As faixas vêm de <see cref="QuizRules"/>, as mesmas
/// constantes que o domínio aplica, para que borda e domínio não possam divergir.
/// </summary>
public sealed class OptionRequestValidator : AbstractValidator<OptionRequest>
{
    public OptionRequestValidator() =>
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Texto da opção é obrigatório.")
            .MaximumLength(QuizRules.OptionTextMaxLength);
}

/// <summary>
/// Forma da pergunta. A regra "exatamente uma opção correta" NÃO está aqui: depende do domínio
/// e vive no handler, conforme a regra de corte de D2.
/// </summary>
public sealed class QuestionRequestValidator : AbstractValidator<QuestionRequest>
{
    public QuestionRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Enunciado é obrigatório.")
            .MaximumLength(QuizRules.QuestionTextMaxLength);

        RuleFor(x => x.TimeLimitSeconds)
            .InclusiveBetween(QuizRules.MinTimeLimitSeconds, QuizRules.MaxTimeLimitSeconds);

        RuleFor(x => x.BasePoints)
            .InclusiveBetween(QuizRules.MinBasePoints, QuizRules.MaxBasePoints);

        RuleFor(x => x.Options)
            .NotNull()
            .Must(static o => o is not null
                && o.Count >= QuizRules.MinOptionsPerQuestion
                && o.Count <= QuizRules.MaxOptionsPerQuestion)
            .WithMessage(
                $"Cada pergunta deve ter entre {QuizRules.MinOptionsPerQuestion} e {QuizRules.MaxOptionsPerQuestion} opções.");

        RuleForEach(x => x.Options).SetValidator(new OptionRequestValidator());
    }
}

public sealed class CreateQuizValidator : Validator<CreateQuizRequest>
{
    public CreateQuizValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Título é obrigatório.")
            .MaximumLength(QuizRules.TitleMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(QuizRules.DescriptionMaxLength)
            .When(static x => x.Description is not null);

        RuleForEach(x => x.Questions).SetValidator(new QuestionRequestValidator());
    }
}

public sealed class UpdateQuizValidator : Validator<UpdateQuizRequest>
{
    public UpdateQuizValidator()
    {
        RuleFor(x => x.QuizId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Título é obrigatório.")
            .MaximumLength(QuizRules.TitleMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(QuizRules.DescriptionMaxLength)
            .When(static x => x.Description is not null);

        RuleForEach(x => x.Questions).SetValidator(new QuestionRequestValidator());
    }
}

internal static class QuizRequestRules
{
    public static List<QuestionInput> ToInputs(List<QuestionRequest> questions) =>
        [.. questions.Select(static q => new QuestionInput(
            q.Text,
            q.TimeLimitSeconds,
            q.BasePoints,
            [.. q.Options.Select(static o => new OptionInput(o.Text, o.IsCorrect))]))];
}
