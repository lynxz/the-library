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
        var (callerUsername, guardError) = await AuthHelper.RequireAdminUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        var (body, parseError) = await FunctionRequests.TryReadJsonAsync<ResetPasswordRequest>(req, "Invalid request body.");
        if (parseError is not null)
        {
            return parseError;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.NewPassword))
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, "New password is required.");
        }

        if (body.NewPassword.Length < 8)
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, "Password must be at least 8 characters.");
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
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.NotFound, "User not found.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.NewPassword);
        await tableClient.UpdateEntityAsync(user, user.ETag, TableUpdateMode.Replace);

        _logger.LogInformation("Admin {Admin} reset password for user: {Username}", callerUsername, targetUsername);

        return await FunctionResponses.Json(req, System.Net.HttpStatusCode.OK, new { message = "Password reset successfully." });
    }
}
