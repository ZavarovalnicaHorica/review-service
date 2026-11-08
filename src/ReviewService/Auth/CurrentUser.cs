using System.Security.Claims;

namespace Horica.ReviewService.Auth;

/// <summary>The employee performing the current request.</summary>
public sealed record CurrentUser(string Id, string? Name, bool IsAdmin);

public static class CurrentUserExtensions
{
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal, string adminRole)
    {
        var id = FirstValue(principal, "sub")
                 ?? FirstValue(principal, ClaimTypes.NameIdentifier)
                 ?? principal.Identity?.Name;

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("The authenticated user has no identifier ('sub') claim.");
        }

        var name = FirstValue(principal, "preferred_username")
                   ?? FirstValue(principal, "name")
                   ?? principal.Identity?.Name;

        return new CurrentUser(id, name, principal.IsInRole(adminRole));
    }

    private static string? FirstValue(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
