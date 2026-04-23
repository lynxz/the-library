using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class GetCurrentUser
{
    private readonly JwtHelper _jwtHelper;

    public GetCurrentUser(JwtHelper jwtHelper)
    {
        _jwtHelper = jwtHelper;
    }

    [Function("me")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequestData req)
    {
        var (username, guardError) = await AuthHelper.RequireAuthenticatedUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        var (_, isAdmin) = AuthHelper.GetAuthenticatedUserWithClaims(req, _jwtHelper);
        return await FunctionResponses.Json(req, System.Net.HttpStatusCode.OK, new { username, isAdmin });
    }
}
