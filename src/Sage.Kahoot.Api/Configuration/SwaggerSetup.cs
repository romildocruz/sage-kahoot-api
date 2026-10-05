using FastEndpoints.Swagger;
using Sage.Kahoot.Infrastructure.Configuration;
using NSwag;

namespace Sage.Kahoot.Api.Configuration;

/// <summary>
/// Documentação OpenAPI pelo gerador nativo do FastEndpoints (NSwag).
/// Sempre disponível em Desenvolvimento e Homologação; em Produção, só com a flag ligada.
/// </summary>
public static class SwaggerSetup
{
    public static bool IsSwaggerEnabled(this WebApplicationBuilder builder) =>
        builder.Environment.IsDevelopment()
        || builder.Environment.IsStaging()
        || builder.Configuration.GetSection(SwaggerOptions.SectionName).Get<SwaggerOptions>()?.Enabled == true;

    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.SwaggerDocument(options =>
        {
            options.DocumentSettings = settings =>
            {
                settings.Title = "sage-kahoot-api";
                settings.Version = "v1";
                settings.Description =
                    "API interna da plataforma de quiz gamificado. " +
                    "Rotas administrativas exigem token de admin; entrada por PIN e envio de resposta são do participante.";

                settings.AddAuth("Bearer", new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Informe o token obtido em POST /api/v1/auth/login."
                });
            };
        });

        return services;
    }
}
