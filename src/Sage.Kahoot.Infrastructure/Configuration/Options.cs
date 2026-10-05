using System.ComponentModel.DataAnnotations;

namespace Sage.Kahoot.Infrastructure.Configuration;

/// <summary>
/// Conexão com o MongoDB. A connection string vem exclusivamente de variável de ambiente
/// mapeada de Secret — nunca de arquivo versionado.
/// </summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Mongo__ConnectionString é obrigatória.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Mongo__Database é obrigatória.")]
    public string Database { get; set; } = string.Empty;

    /// <summary>Timeout do ping de readiness. Curto de propósito: o probe não deve segurar o pod.</summary>
    [Range(100, 10_000)]
    public int HealthCheckTimeoutMs { get; set; } = 2_000;
}

/// <summary>Credencial única do admin e parâmetros do token (D6).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Comprimento mínimo da chave para HS256.</summary>
    public const int MinimumSigningKeyBytes = 32;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Auth__AdminUsername é obrigatório.")]
    public string AdminUsername { get; set; } = string.Empty;

    /// <summary>Sem valor padrão embutido: vem apenas de Secret/variável de ambiente.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Auth__AdminPassword é obrigatória (forneça via Secret).")]
    public string AdminPassword { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Auth__SigningKey é obrigatória (forneça via Secret).")]
    public string SigningKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "sage-kahoot-api";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "sage-kahoot-web";

    [Range(1, 24)]
    public int AdminTokenLifetimeHours { get; set; } = 8;

    /// <summary>Margem além do fim previsto da sessão, para o token do participante não expirar durante o jogo.</summary>
    [Range(1, 24)]
    public int ParticipantTokenLifetimeHours { get; set; } = 6;
}

/// <summary>Origens autorizadas. Curinga não é aceito em nenhum ambiente.</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "sage-kahoot-web";

    [MinLength(1, ErrorMessage = "Cors__AllowedOrigins deve listar ao menos uma origem.")]
    public string[] AllowedOrigins { get; set; } = [];
}

/// <summary>Limites por IP nos dois endpoints públicos sensíveis a abuso.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public const string LoginPolicy = "login";
    public const string JoinPolicy = "join";

    [Range(1, 1_000)]
    public int LoginPermitLimit { get; set; } = 5;

    [Range(1, 3_600)]
    public int LoginWindowSeconds { get; set; } = 60;

    [Range(1, 1_000)]
    public int JoinPermitLimit { get; set; } = 20;

    [Range(1, 3_600)]
    public int JoinWindowSeconds { get; set; } = 60;
}

/// <summary>Exposição da documentação interativa. Em Produção o padrão é desabilitado.</summary>
public sealed class SwaggerOptions
{
    public const string SectionName = "Swagger";

    public bool Enabled { get; set; }
}

/// <summary>Carga inicial sintética.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }
}
