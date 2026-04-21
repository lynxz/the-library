using Microsoft.Azure.Functions.Worker.Http;

namespace TheLibrary.Api.Services;

public static class AuthHelper
{
    public static string? GetAuthenticatedUser(HttpRequestData req, JwtHelper jwtHelper)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values))
            return null;

        var header = values.FirstOrDefault();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header["Bearer ".Length..].Trim();
        return jwtHelper.ValidateToken(token);
    }

    public static (string? Username, bool IsAdmin) GetAuthenticatedUserWithClaims(HttpRequestData req, JwtHelper jwtHelper)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values))
            return (null, false);

        var header = values.FirstOrDefault();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (null, false);

        var token = header["Bearer ".Length..].Trim();
        return jwtHelper.ValidateTokenWithClaims(token);
    }
}
