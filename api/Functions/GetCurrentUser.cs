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
        var (username, isAdmin) = AuthHelper.GetAuthenticatedUserWithClaims(req, _jwtHelper);

        if (username is null)
        {
            var unauthorized = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Not authenticated." });
            return unauthorized;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { username, isAdmin });
        return response;
    }
}
