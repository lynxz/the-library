using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class CreateUser
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<CreateUser> _logger;

    public CreateUser(TableServiceClient tableServiceClient, JwtHelper jwtHelper, ILogger<CreateUser> logger)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    private record CreateUserRequest(string Username, string Password);

    [Function("createUser")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "useradmin/users")] HttpRequestData req)
    {
        var (callerUsername, guardError) = await AuthHelper.RequireAdminUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        var (body, parseError) = await FunctionRequests.TryReadJsonAsync<CreateUserRequest>(req, "Invalid request body.");
        if (parseError is not null)
        {
            return parseError;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, "Username and password are required.");
        }

        if (body.Password.Length < 8)
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, "Password must be at least 8 characters.");
        }

        var tableClient = _tableServiceClient.GetTableClient("Users");

        // Check if user already exists
        try
        {
            await tableClient.GetEntityAsync<UserAccount>("USER", body.Username);
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.Conflict, "A user with that username already exists.");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // User does not exist — proceed
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(body.Password);

        var newUser = new UserAccount
        {
            PartitionKey = "USER",
            RowKey = body.Username,
            PasswordHash = passwordHash,
            IsAdmin = false
        };

        await tableClient.AddEntityAsync(newUser);
        _logger.LogInformation("Admin {Admin} created user: {Username}", callerUsername, body.Username);

        return await FunctionResponses.Json(req, System.Net.HttpStatusCode.Created, new { username = body.Username });
    }
}
