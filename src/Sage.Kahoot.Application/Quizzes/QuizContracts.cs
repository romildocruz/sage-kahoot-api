using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Quizzes;

namespace Sage.Kahoot.Application.Quizzes;

/// <summary>Opção como chega do apresentador, ainda sem identificador.</summary>
public sealed record OptionInput(string Text, bool IsCorrect);

/// <summary>Pergunta como chega do apresentador. A ordem é a posição na lista.</summary>
public sealed record QuestionInput(
    string Text,
    int TimeLimitSeconds,
    int BasePoints,
    IReadOnlyList<OptionInput> Options);

public sealed record OptionDto(Guid Id, string Text, bool IsCorrect);

public sealed record QuestionDto(
    Guid Id,
    int Order,
    string Text,
    int TimeLimitSeconds,
    int BasePoints,
    IReadOnlyList<OptionDto> Options);

/// <summary>
/// Representação completa do quiz para o apresentador — inclui a marcação da opção correta,
/// o que é adequado porque estas rotas são exclusivas do admin.
/// </summary>
public sealed record QuizDto(
    Guid Id,
    string Title,
    string? Description,
    IReadOnlyList<QuestionDto> Questions,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public static class QuizErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("quiz.not_found", "Quiz não encontrado.");

    /// <summary>
    /// Impede que o conteúdo mude no meio do treinamento (spec quiz-management,
    /// "Edição de quiz em uso por sessão ativa").
    /// </summary>
    public static readonly DomainError InUseByActiveSession =
        DomainError.Conflict(
            "quiz.in_use_by_active_session",
            "O quiz está vinculado a uma sessão ativa e não pode ser alterado ou excluído.");
}

/// <summary>Conversões entre o agregado e os contratos da camada de aplicação.</summary>
public static class QuizMapper
{
    public static QuizDto ToDto(Quiz quiz) =>
        new(quiz.Id,
            quiz.Title,
            quiz.Description,
            [.. quiz.Questions.Select(ToDto)],
            quiz.CreatedAtUtc,
            quiz.UpdatedAtUtc);

    public static QuestionDto ToDto(Question question) =>
        new(question.Id,
            question.Order,
            question.Text,
            question.TimeLimitSeconds,
            question.BasePoints,
            [.. question.Options.Select(static o => new OptionDto(o.Id, o.Text, o.IsCorrect))]);

    /// <summary>
    /// Constrói as perguntas do domínio a partir da entrada, parando no primeiro erro.
    /// Nada é persistido quando qualquer pergunta é inválida (all-or-nothing).
    /// </summary>
    public static Result<List<Question>> BuildQuestions(IReadOnlyList<QuestionInput> inputs)
    {
        var questions = new List<Question>(inputs.Count);

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var options = new List<AnswerOption>(input.Options.Count);

            foreach (var optionInput in input.Options)
            {
                var option = AnswerOption.Create(optionInput.Text, optionInput.IsCorrect);

                if (!option.IsSuccess)
                {
                    return Result<List<Question>>.Failure(option.Error!);
                }

                options.Add(option.Value);
            }

            var question = Question.Create(
                input.Text,
                index,
                input.TimeLimitSeconds,
                input.BasePoints,
                options);

            if (!question.IsSuccess)
            {
                return Result<List<Question>>.Failure(question.Error!);
            }

            questions.Add(question.Value);
        }

        return questions;
    }
}
