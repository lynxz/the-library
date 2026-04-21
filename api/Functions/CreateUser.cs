using System.Text.Json;
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
        var (callerUsername, isAdmin) = AuthHelper.GetAuthenticatedUserWithClaims(req, _jwtHelper);

        if (callerUsername is null)
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

        CreateUserRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateUserRequest>(req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid request body." });
            return badRequest;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Username and password are required." });
            return badRequest;
        }

        if (body.Password.Length < 8)
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Password must be at least 8 characters." });
            return badRequest;
        }

        var tableClient = _tableServiceClient.GetTableClient("Users");

        // Check if user already exists
        try
        {
            await tableClient.GetEntityAsync<UserAccount>("USER", body.Username);
            var conflict = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new { error = "A user with that username already exists." });
            return conflict;
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

        var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { username = body.Username });
        return response;
    }
}
