using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace TheLibrary.Api.Services;

public static class FunctionRequests
{
    public static async Task<(T? Value, HttpResponseData? ErrorResponse)> TryReadJsonAsync<T>(HttpRequestData req, string invalidBodyMessage)
    {
        try
        {
            var value = await JsonSerializer.DeserializeAsync<T>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return (value, null);
        }
        catch
        {
            var error = await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, invalidBodyMessage);
            return (default, error);
        }
    }
}