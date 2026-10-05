namespace Sage.Kahoot.Domain.Sessions;

/// <summary>
/// Pontuação por acerto e velocidade. Determinística e reproduzível a partir dos dados persistidos:
/// depende apenas da pontuação base, do tempo limite e do tempo de resposta medido pelo servidor.
/// </summary>
public static class Scoring
{
    /// <summary>
    /// Erro pontua 0. Acerto decresce linearmente de 100% da base (resposta instantânea)
    /// a 50% da base (resposta no limite do tempo).
    /// </summary>
    public static int Calculate(bool isCorrect, long responseTimeMs, int timeLimitSeconds, int basePoints)
    {
        if (!isCorrect)
        {
            return 0;
        }

        if (timeLimitSeconds <= 0)
        {
            return basePoints;
        }

        var windowMs = (double)timeLimitSeconds * 1000d;
        var elapsedRatio = Math.Clamp(responseTimeMs / windowMs, 0d, 1d);
        var factor = 1d - ((1d - SessionRules.MinimumCorrectAnswerFactor) * elapsedRatio);

        return (int)Math.Round(basePoints * factor, MidpointRounding.AwayFromZero);
    }
}
