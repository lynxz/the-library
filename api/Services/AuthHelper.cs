using Microsoft.Azure.Functions.Worker.Http;

namespace TheLibrary.Api.Services;

public static class AuthHelper
{
    public static async Task<(string? Username, HttpResponseData? ErrorResponse)> RequireAuthenticatedUser(HttpRequestData req, JwtHelper jwtHelper)
    {
        var username = GetAuthenticatedUser(req, jwtHelper);
        if (username is not null)
        {
            return (username, null);
        }

        return (null, await FunctionResponses.Error(req, System.Net.HttpStatusCode.Unauthorized, "Not authenticated."));
    }

    public static async Task<(string? Username, HttpResponseData? ErrorResponse)> RequireAdminUser(HttpRequestData req, JwtHelper jwtHelper)
    {
        var (username, isAdmin) = GetAuthenticatedUserWithClaims(req, jwtHelper);

        if (username is null)
        {
            return (null, await FunctionResponses.Error(req, System.Net.HttpStatusCode.Unauthorized, "Not authenticated."));
        }

        if (!isAdmin)
        {
            return (null, await FunctionResponses.Error(req, System.Net.HttpStatusCode.Forbidden, "Admin access required."));
        }

        return (username, null);
    }

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
