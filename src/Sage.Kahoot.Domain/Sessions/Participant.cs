namespace Sage.Kahoot.Domain.Sessions;

/// <summary>
/// Participante anônimo da sessão. Persiste apenas apelido e identificador opaco — nunca dado pessoal real.
/// </summary>
public sealed class Participant
{
    internal Participant(Guid id, string nickname, int joinOrder, DateTime joinedAtUtc)
    {
        Id = id;
        Nickname = nickname;
        JoinOrder = joinOrder;
        JoinedAtUtc = joinedAtUtc;
        IsConnected = true;
    }

    public Guid Id { get; }

    public string Nickname { get; }

    /// <summary>Ordem de entrada na sessão. Último critério de desempate do ranking.</summary>
    public int JoinOrder { get; }

    public DateTime JoinedAtUtc { get; }

    /// <summary>
    /// Pontuação acumulada desnormalizada. Cache reconstruível: a fonte de verdade é a coleção de respostas.
    /// </summary>
    public int TotalScore { get; private set; }

    /// <summary>Soma dos tempos de resposta. Primeiro critério de desempate do ranking.</summary>
    public long TotalResponseTimeMs { get; private set; }

    public bool IsConnected { get; private set; }

    public void AddScore(int points, long responseTimeMs)
    {
        TotalScore += points;
        TotalResponseTimeMs += responseTimeMs;
    }

    public void SetConnected(bool connected) => IsConnected = connected;

    public static Participant Rehydrate(
        Guid id,
        string nickname,
        int joinOrder,
        DateTime joinedAtUtc,
        int totalScore,
        long totalResponseTimeMs,
        bool isConnected)
    {
        var participant = new Participant(id, nickname, joinOrder, joinedAtUtc);
        participant.TotalScore = totalScore;
        participant.TotalResponseTimeMs = totalResponseTimeMs;
        participant.IsConnected = isConnected;
        return participant;
    }
}
