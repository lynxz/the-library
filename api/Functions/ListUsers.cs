using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class ListUsers
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;

    public ListUsers(TableServiceClient tableServiceClient, JwtHelper jwtHelper)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
    }

    [Function("listUsers")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "useradmin/users")] HttpRequestData req)
    {
        var (username, isAdmin) = AuthHelper.GetAuthenticatedUserWithClaims(req, _jwtHelper);

        if (username is null)
        {
            var unauthorized = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Not authenticated." });
            return unauthorized;
        }

        if (!isAdmin)
        {
            var forbidden = req.CreateResponse(System.Net.HttpStatusCode.Forbidden);
            await forbidden.WriteAsJsonAsync(new { error = "Admin access required." });
            return forbidden;
        }

        var tableClient = _tableServiceClient.GetTableClient("Users");
        var users = new List<object>();

        await foreach (var entity in tableClient.QueryAsync<UserAccount>(u => u.PartitionKey == "USER"))
        {
            users.Add(new { username = entity.RowKey, isAdmin = entity.IsAdmin });
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(users);
        return response;
    }
}
