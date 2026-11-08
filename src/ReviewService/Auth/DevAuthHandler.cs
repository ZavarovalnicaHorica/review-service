using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Horica.ReviewService.Auth;

/// <summary>
/// Development-only authentication (Auth:Mode = Disabled). Every request is treated as an employee.
/// Use the headers X-Dev-User and X-Dev-Roles (comma separated) to pretend to be somebody else.
/// Defaults: user "dev-reviewer", role "reviewer".
/// </summary>
public sealed class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevAuth";

    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userName = ReadHeader("X-Dev-User") ?? "dev-reviewer";
        var rolesHeader = ReadHeader("X-Dev-Roles") ?? "reviewer";

        var claims = new List<Claim>
        {
            new Claim("sub", userName),
            new Claim("preferred_username", userName),
            new Claim(ClaimTypes.Name, userName)
        };

        foreach (var role in rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? ReadHeader(string name)
    {
        var value = Request.Headers[name].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
