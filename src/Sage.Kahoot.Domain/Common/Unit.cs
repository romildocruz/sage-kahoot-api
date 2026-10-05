namespace Sage.Kahoot.Domain.Common;

/// <summary>
/// Resultado vazio. Permite que todo handler retorne <see cref="Result{T}"/>, inclusive os que não produzem valor,
/// mantendo um único caminho de tradução para HTTP.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
