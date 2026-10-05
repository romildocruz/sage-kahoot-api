namespace Sage.Kahoot.Application.Abstractions;

/// <summary>
/// Relógio do servidor. Única autoridade de tempo do sistema: nenhum valor de tempo enviado
/// pelo cliente é aceito para fins de pontuação.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
