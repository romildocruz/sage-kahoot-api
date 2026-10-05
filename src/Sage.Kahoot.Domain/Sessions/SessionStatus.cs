namespace Sage.Kahoot.Domain.Sessions;

/// <summary>
/// Estados da sessão, nomeados como na spec live-session para manter a linguagem ubíqua.
/// Transições permitidas: Aguardando -> EmAndamento -> Encerrada; Aguardando/EmAndamento -> Cancelada.
/// </summary>
public enum SessionStatus
{
    Aguardando = 0,
    EmAndamento = 1,
    Encerrada = 2,
    Cancelada = 3
}

public static class SessionStatusExtensions
{
    /// <summary>Sessões que ainda ocupam um PIN e bloqueiam edição do quiz de origem.</summary>
    public static bool IsActive(this SessionStatus status) =>
        status is SessionStatus.Aguardando or SessionStatus.EmAndamento;
}
