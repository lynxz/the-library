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
        var (_, guardError) = await AuthHelper.RequireAdminUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        var tableClient = _tableServiceClient.GetTableClient("Users");
        var users = new List<object>();

        await foreach (var entity in tableClient.QueryAsync<UserAccount>(u => u.PartitionKey == "USER"))
        {
            users.Add(new { username = entity.RowKey, isAdmin = entity.IsAdmin });
        }

        return await FunctionResponses.Json(req, System.Net.HttpStatusCode.OK, users);
    }
}
