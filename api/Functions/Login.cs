using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class Login
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<Login> _logger;

    public Login(TableServiceClient tableServiceClient, JwtHelper jwtHelper, ILogger<Login> logger)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    private record LoginRequest(string Username, string Password);

    [Function("login")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequestData req)
    {
        LoginRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<LoginRequest>(req.Body,
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

        var tableClient = _tableServiceClient.GetTableClient("Users");

        UserAccount? user;
        try
        {
            var result = await tableClient.GetEntityAsync<UserAccount>("USER", body.Username);
            user = result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            user = null;
        }

        if (user is null || !BCrypt.Net.BCrypt.Verify(body.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", body.Username);
            var unauthorized = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Invalid username or password." });
            return unauthorized;
        }

        _logger.LogInformation("Successful login for user: {Username}", body.Username);

        var token = _jwtHelper.GenerateToken(body.Username, user.IsAdmin);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { token, username = body.Username, isAdmin = user.IsAdmin });
        return response;
    }
}
