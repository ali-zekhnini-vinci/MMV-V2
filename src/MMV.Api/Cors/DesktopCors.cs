namespace MMV.Api.Cors;

/// <summary>
/// CORS du client Tauri : origines explicites lues dans <c>Cors:AllowedOrigins</c> (définies en
/// développement seulement), sans identifiants (aucun cookie). Deux politiques sur les mêmes origines :
/// <see cref="PolicyName"/> (santé, GET seul, 01B) et <see cref="ApiPolicyName"/> (routes <c>/api</c>,
/// phase 02 : GET/POST ; phase 03-A2 : + PUT pour la modification d'un client ; en-têtes <c>Authorization</c>
/// et <c>Content-Type</c> seulement, jamais DELETE). Toute origine
/// joker ou non absolue fait échouer le démarrage plutôt que d'ouvrir l'API.
/// </summary>
internal static class DesktopCors
{
    public const string PolicyName = "desktop";
    public const string ApiPolicyName = "desktop-api";
    public const string OriginsKey = "Cors:AllowedOrigins";

    public static IServiceCollection AddDesktopCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(OriginsKey).Get<string[]>() ?? [];
        foreach (var origin in origins)
        {
            if (origin.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.GetLeftPart(UriPartial.Authority) != origin)
            {
                throw new InvalidOperationException(
                    $"Origine CORS invalide dans '{OriginsKey}' : origine exacte attendue (schéma://hôte[:port]).");
            }
        }

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Get));
            options.AddPolicy(ApiPolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put)
                .WithHeaders("Authorization", "Content-Type"));
        });

        return services;
    }
}
