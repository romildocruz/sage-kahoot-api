namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Gera o PIN numérico da sessão a partir de fonte criptograficamente segura.</summary>
public interface IPinGenerator
{
    string Generate();
}
