using Sage.Kahoot.Domain.Quizzes;

namespace Sage.Kahoot.UnitTests.Domain;

/// <summary>Cobre a spec quiz-management, requisito "Estrutura e validação da pergunta".</summary>
public sealed class QuizTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private static AnswerOption Option(string text, bool correct) => AnswerOption.Create(text, correct).Value;

    private static List<AnswerOption> ValidOptions() =>
        [Option("Alternativa A", true), Option("Alternativa B", false)];

    [Fact]
    public void Pergunta_sem_opcao_correta_e_rejeitada()
    {
        var result = Question.Create("Enunciado", 0, 20, 1000,
            [Option("A", false), Option("B", false)]);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.correct_option_count", result.Error!.Code);
    }

    [Fact]
    public void Pergunta_com_duas_opcoes_corretas_e_rejeitada()
    {
        var result = Question.Create("Enunciado", 0, 20, 1000,
            [Option("A", true), Option("B", true)]);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.correct_option_count", result.Error!.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Numero_de_opcoes_fora_da_faixa_e_rejeitado(int optionCount)
    {
        var options = Enumerable.Range(0, optionCount)
            .Select(i => Option($"Opção {i}", i == 0))
            .ToList();

        var result = Question.Create("Enunciado", 0, 20, 1000, options);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.option_count_out_of_range", result.Error!.Code);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public void Tempo_limite_fora_da_faixa_e_rejeitado(int timeLimit)
    {
        var result = Question.Create("Enunciado", 0, timeLimit, 1000, ValidOptions());

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.time_limit_out_of_range", result.Error!.Code);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(2001)]
    public void Pontuacao_base_fora_da_faixa_e_rejeitada(int basePoints)
    {
        var result = Question.Create("Enunciado", 0, 20, basePoints, ValidOptions());

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.base_points_out_of_range", result.Error!.Code);
    }

    [Fact]
    public void Opcoes_duplicadas_na_mesma_pergunta_sao_rejeitadas()
    {
        var result = Question.Create("Enunciado", 0, 20, 1000,
            [Option("Mesma resposta", true), Option("mesma resposta", false)]);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.question.duplicate_option_text", result.Error!.Code);
    }

    [Fact]
    public void Quiz_sem_titulo_e_rejeitado()
    {
        var result = Quiz.Create("   ", null, [], Now);

        Assert.False(result.IsSuccess);
        Assert.Equal("quiz.title_required", result.Error!.Code);
    }

    [Fact]
    public void Quiz_pode_ser_criado_sem_perguntas()
    {
        var result = Quiz.Create("Treinamento de integração", null, [], Now);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Questions);
    }

    [Fact]
    public void Ordem_das_perguntas_e_normalizada_pelo_agregado()
    {
        var questions = new[]
        {
            Question.Create("Primeira", 7, 20, 1000, ValidOptions()).Value,
            Question.Create("Segunda", 3, 20, 1000, ValidOptions()).Value
        };

        var quiz = Quiz.Create("Quiz", null, questions, Now).Value;

        Assert.Equal([0, 1], quiz.Questions.Select(q => q.Order));
    }

    [Fact]
    public void Duplicacao_gera_copia_independente_com_novos_identificadores()
    {
        var original = Quiz.Create(
            "Quiz original",
            "Descrição",
            [Question.Create("Pergunta", 0, 20, 1000, ValidOptions()).Value],
            Now).Value;

        var copy = original.Duplicate("Quiz original (cópia)", Now);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.NotEqual(original.Questions[0].Id, copy.Questions[0].Id);
        Assert.NotEqual(original.Questions[0].Options[0].Id, copy.Questions[0].Options[0].Id);
        Assert.Equal(original.Questions[0].Text, copy.Questions[0].Text);
    }

    [Fact]
    public void Edicao_com_pergunta_invalida_nao_altera_o_estado_do_agregado()
    {
        var quiz = Quiz.Create(
            "Título original",
            null,
            [Question.Create("Pergunta", 0, 20, 1000, ValidOptions()).Value],
            Now).Value;

        var result = quiz.Update(string.Empty, null, [], Now.AddMinutes(5));

        Assert.False(result.IsSuccess);
        Assert.Equal("Título original", quiz.Title);
        Assert.Single(quiz.Questions);
    }
}
