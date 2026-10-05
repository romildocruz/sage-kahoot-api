using System.Collections.Concurrent;

namespace Sage.Kahoot.Application.Sessions.ParticipantPresence;

/// <summary>
/// Conta as conexões de tempo real abertas por participante.
/// </summary>
/// <remarks>
/// Existe porque o mesmo participante pode abrir duas abas: tratar a primeira queda como saída
/// emitiria evento falso e o tiraria da contagem de ativos, fazendo a pergunta fechar cedo demais.
/// O estado vive no processo porque o deployment é fixo em uma réplica (design.md — D1); a
/// persistência de <c>IsConnected</c> é o registro consultado pela lógica de fechamento.
/// Consequência assumida: um reinício zera as contagens, e cada participante volta a ser contado
/// na próxima conexão.
/// </remarks>
public sealed class ParticipantConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    /// <summary>
    /// Registra mais uma conexão. Retorna verdadeiro quando é a primeira do participante —
    /// ou seja, quando ele acabou de ficar ativo.
    /// </summary>
    public bool Acquire(Guid participantId)
    {
        var count = _connections.AddOrUpdate(participantId, 1, static (_, current) => current + 1);
        return count == 1;
    }

    /// <summary>
    /// Libera uma conexão. Retorna verdadeiro somente quando era a última do participante —
    /// é esse retorno que autoriza anunciar a saída.
    /// </summary>
    public bool Release(Guid participantId)
    {
        while (true)
        {
            if (!_connections.TryGetValue(participantId, out var current))
            {
                // Sem registro: ou o processo reiniciou, ou a conexão nunca foi contada.
                // Tratar como última é o lado seguro — marca inativo quem de fato saiu.
                return true;
            }

            if (current <= 1)
            {
                if (_connections.TryRemove(new KeyValuePair<Guid, int>(participantId, current)))
                {
                    return true;
                }

                continue;
            }

            if (_connections.TryUpdate(participantId, current - 1, current))
            {
                return false;
            }
        }
    }

    /// <summary>Conexões abertas do participante. Usado em teste e diagnóstico.</summary>
    public int OpenConnections(Guid participantId) =>
        _connections.TryGetValue(participantId, out var count) ? count : 0;
}
