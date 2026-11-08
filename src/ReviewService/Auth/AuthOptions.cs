namespace Horica.ReviewService.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>"Jwt" (default; validates Keycloak/OIDC tokens) or "Disabled" (Development only).</summary>
    public string Mode { get; set; } = "Jwt";

    /// <summary>OIDC authority, e.g. https://keycloak.example.com/realms/horica</summary>
    public string? Authority { get; set; }

    /// <summary>Optional. When set, tokens must contain this audience.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Optional. Use when the service reaches Keycloak at a different URL than the one in the token issuer
    /// (e.g. inside Docker). Example: http://keycloak:8080/realms/horica/.well-known/openid-configuration
    /// </summary>
    public string? MetadataAddress { get; set; }

    /// <summary>Optional. Overrides the expected token issuer (the "iss" claim).</summary>
    public string? ValidIssuer { get; set; }

    /// <summary>Optional. Keycloak client id whose client roles (resource_access) should also be read.</summary>
    public string? ClientId { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Role that allows an employee to work on reviews.</summary>
    public string ReviewerRole { get; set; } = "reviewer";

    /// <summary>Role that may also take over or release reviews of other employees.</summary>
    public string AdminRole { get; set; } = "admin";

    public bool IsDisabled => string.Equals(Mode, "Disabled", StringComparison.OrdinalIgnoreCase);
}
