using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.UnitTests.Domain;

/// <summary>Cobre a spec live-session, requisito "Pontuação por acerto e velocidade".</summary>
public sealed class ScoringTests
{
    [Fact]
    public void Acerto_instantaneo_recebe_a_pontuacao_base_integral()
    {
        var points = Scoring.Calculate(isCorrect: true, responseTimeMs: 0, timeLimitSeconds: 20, basePoints: 1000);

        Assert.Equal(1000, points);
    }

    [Fact]
    public void Acerto_no_limite_do_tempo_recebe_metade_da_base()
    {
        var points = Scoring.Calculate(isCorrect: true, responseTimeMs: 20_000, timeLimitSeconds: 20, basePoints: 1000);

        Assert.Equal(500, points);
    }

    [Fact]
    public void Acerto_na_metade_do_tempo_recebe_75_por_cento_da_base()
    {
        var points = Scoring.Calculate(isCorrect: true, responseTimeMs: 10_000, timeLimitSeconds: 20, basePoints: 1000);

        Assert.Equal(750, points);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5_000)]
    [InlineData(20_000)]
    public void Erro_pontua_zero_independentemente_do_tempo(long responseTimeMs)
    {
        var points = Scoring.Calculate(isCorrect: false, responseTimeMs, timeLimitSeconds: 20, basePoints: 1000);

        Assert.Equal(0, points);
    }

    [Fact]
    public void Resposta_depois_do_limite_nao_pontua_abaixo_do_piso()
    {
        // A tolerância de rede pode deixar o tempo passar um pouco do limite:
        // o fator satura em 50%, nunca abaixo.
        var points = Scoring.Calculate(isCorrect: true, responseTimeMs: 25_000, timeLimitSeconds: 20, basePoints: 1000);

        Assert.Equal(500, points);
    }

    [Fact]
    public void Pontuacao_e_deterministica_para_a_mesma_entrada()
    {
        var first = Scoring.Calculate(true, 7_321, 15, 800);
        var second = Scoring.Calculate(true, 7_321, 15, 800);

        Assert.Equal(first, second);
    }
}
