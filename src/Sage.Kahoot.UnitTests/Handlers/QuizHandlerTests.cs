using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Application.Quizzes.CreateQuiz;
using Sage.Kahoot.Application.Quizzes.DeleteQuiz;
using Sage.Kahoot.Application.Quizzes.DuplicateQuiz;
using Sage.Kahoot.Application.Quizzes.UpdateQuiz;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.UnitTests.Handlers;

/// <summary>Cobre quiz-management no nível do caso de uso, incluindo o bloqueio por sessão ativa.</summary>
public sealed class QuizHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryQuizRepository _quizzes = new();
    private readonly InMemorySessionRepository _sessions = new();
    private readonly FixedClock _clock = new(Now);

    private static List<QuestionInput> ValidQuestions() =>
    [
        new("Pergunta 1", 20, 1000, [new OptionInput("Certa", true), new OptionInput("Errada", false)])
    ];

    [Fact]
    public async Task Criar_quiz_valido_persiste_e_devolve_a_representacao()
    {
        var handler = new CreateQuizHandler(_quizzes, _clock);

        var result = await handler.ExecuteAsync(
            new CreateQuizCommand("Treinamento", "Descrição", ValidQuestions()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_quizzes.Items);
        Assert.Equal("Treinamento", result.Value.Title);
        Assert.Single(result.Value.Questions);
    }

    [Fact]
    public async Task Criar_quiz_com_duas_opcoes_corretas_falha_e_nao_persiste()
    {
        var handler = new CreateQuizHandler(_quizzes, _clock);

        var result = await handler.ExecuteAsync(
            new CreateQuizCommand("Treinamento", null,
                [new QuestionInput("Pergunta", 20, 1000,
                    [new OptionInput("A", true), new OptionInput("B", true)])]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.correct_option_count", result.Error!.Code);
        Assert.Empty(_quizzes.Items);
    }

    [Fact]
    public async Task Editar_quiz_com_sessao_ativa_retorna_conflito_e_nao_altera_nada()
    {
        var quiz = await CreateQuizAsync();
        await OpenActiveSessionForAsync(quiz.Id);

        var handler = new UpdateQuizHandler(_quizzes, _sessions, _clock);

        var result = await handler.ExecuteAsync(
            new UpdateQuizCommand(quiz.Id, "Título novo", null, ValidQuestions()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategory.Conflict, result.Error!.Category);
        Assert.Equal("quiz.in_use_by_active_session", result.Error.Code);
        Assert.Equal("Treinamento", _quizzes.Items[quiz.Id].Title);
    }

    [Fact]
    public async Task Excluir_quiz_com_sessao_ativa_retorna_conflito()
    {
        var quiz = await CreateQuizAsync();
        await OpenActiveSessionForAsync(quiz.Id);

        var handler = new DeleteQuizHandler(_quizzes, _sessions);

        var result = await handler.ExecuteAsync(new DeleteQuizCommand(quiz.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.in_use_by_active_session", result.Error!.Code);
        Assert.True(_quizzes.Items.ContainsKey(quiz.Id));
    }

    [Fact]
    public async Task Excluir_quiz_sem_sessao_ativa_remove_o_registro()
    {
        var quiz = await CreateQuizAsync();
        var handler = new DeleteQuizHandler(_quizzes, _sessions);

        var result = await handler.ExecuteAsync(new DeleteQuizCommand(quiz.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_quizzes.Items);
    }

    [Fact]
    public async Task Excluir_quiz_inexistente_retorna_nao_encontrado()
    {
        var handler = new DeleteQuizHandler(_quizzes, _sessions);

        var result = await handler.ExecuteAsync(new DeleteQuizCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategory.NotFound, result.Error!.Category);
    }

    [Fact]
    public async Task Duplicar_gera_quiz_independente_e_editar_a_copia_nao_afeta_o_original()
    {
        var original = await CreateQuizAsync();
        var duplicateHandler = new DuplicateQuizHandler(_quizzes, _clock);

        var copy = await duplicateHandler.ExecuteAsync(new DuplicateQuizCommand(original.Id), CancellationToken.None);

        Assert.True(copy.IsSuccess);
        Assert.NotEqual(original.Id, copy.Value.Id);
        Assert.Contains("cópia", copy.Value.Title, StringComparison.OrdinalIgnoreCase);

        var updateHandler = new UpdateQuizHandler(_quizzes, _sessions, _clock);

        await updateHandler.ExecuteAsync(
            new UpdateQuizCommand(copy.Value.Id, "Cópia editada", null,
                [new QuestionInput("Outra pergunta", 30, 500,
                    [new OptionInput("X", true), new OptionInput("Y", false)])]),
            CancellationToken.None);

        Assert.Equal("Treinamento", _quizzes.Items[original.Id].Title);
        Assert.Equal("Pergunta 1", _quizzes.Items[original.Id].Questions[0].Text);
    }

    private async Task<QuizDto> CreateQuizAsync()
    {
        var handler = new CreateQuizHandler(_quizzes, _clock);

        var result = await handler.ExecuteAsync(
            new CreateQuizCommand("Treinamento", null, ValidQuestions()),
            CancellationToken.None);

        return result.Value;
    }

    private async Task OpenActiveSessionForAsync(Guid quizId)
    {
        var quiz = _quizzes.Items[quizId];

        var snapshot = new QuizSnapshot(
            quiz.Id,
            quiz.Title,
            [.. quiz.Questions.Select(q => new SessionQuestion(
                q.Id, q.Order, q.Text, q.TimeLimitSeconds, q.BasePoints,
                [.. q.Options.Select(o => new SessionAnswerOption(o.Id, o.Text, o.IsCorrect))]))]);

        await _sessions.InsertAsync(Session.Open(snapshot, "123456", Now).Value, CancellationToken.None);
    }
}
