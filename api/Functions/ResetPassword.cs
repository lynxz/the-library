using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class ResetPassword
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<ResetPassword> _logger;

    public ResetPassword(TableServiceClient tableServiceClient, JwtHelper jwtHelper, ILogger<ResetPassword> logger)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    private record ResetPasswordRequest(string NewPassword);

    [Function("resetPassword")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "useradmin/users/{targetUsername}/reset-password")] HttpRequestData req,
        string targetUsername)
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

        ResetPasswordRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ResetPasswordRequest>(req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid request body." });
            return badRequest;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.NewPassword))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "New password is required." });
            return badRequest;
        }

        if (body.NewPassword.Length < 8)
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Password must be at least 8 characters." });
            return badRequest;
        }

        var tableClient = _tableServiceClient.GetTableClient("Users");

        UserAccount? user;
        try
        {
            var result = await tableClient.GetEntityAsync<UserAccount>("USER", targetUsername);
            user = result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "User not found." });
            return notFound;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.NewPassword);
        await tableClient.UpdateEntityAsync(user, user.ETag, TableUpdateMode.Replace);

        _logger.LogInformation("Admin {Admin} reset password for user: {Username}", callerUsername, targetUsername);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { message = "Password reset successfully." });
        return response;
    }
}
