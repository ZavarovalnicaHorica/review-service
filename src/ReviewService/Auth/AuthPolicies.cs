namespace Horica.ReviewService.Auth;

public static class AuthPolicies
{
    /// <summary>Authenticated user with the reviewer (or admin) role.</summary>
    public const string Reviewer = "Reviewer";
}
