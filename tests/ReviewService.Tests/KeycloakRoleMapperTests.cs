using Horica.ReviewService.Auth;
using Xunit;

namespace Horica.ReviewService.Tests;

public class KeycloakRoleMapperTests
{
    [Fact]
    public void Reads_realm_roles()
    {
        var roles = KeycloakRoleMapper.ExtractRoles("""{"roles":["reviewer","offline_access"]}""", null, null);

        Assert.Contains("reviewer", roles);
        Assert.Contains("offline_access", roles);
        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public void Reads_client_roles_only_for_the_configured_client()
    {
        const string resourceAccess = """{"horica-ui":{"roles":["admin"]},"other":{"roles":["secret"]}}""";

        var roles = KeycloakRoleMapper.ExtractRoles(null, resourceAccess, "horica-ui");

        Assert.Contains("admin", roles);
        Assert.DoesNotContain("secret", roles);
    }

    [Fact]
    public void Ignores_client_roles_when_no_client_is_configured()
    {
        var roles = KeycloakRoleMapper.ExtractRoles(null, """{"horica-ui":{"roles":["admin"]}}""", null);

        Assert.Empty(roles);
    }

    [Fact]
    public void Ignores_malformed_claims()
    {
        var roles = KeycloakRoleMapper.ExtractRoles("{not json", "[1,2]", "horica-ui");

        Assert.Empty(roles);
    }

    [Fact]
    public void Maps_plain_roles_claim_from_auth_service()
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim("sub", "42"), new System.Security.Claims.Claim("roles", "REVIEWER") },
            "Bearer");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        KeycloakRoleMapper.AddRoleClaims(principal, null);

        Assert.True(principal.IsInRole("REVIEWER"));
        Assert.False(principal.IsInRole("ADMIN"));
    }
}
