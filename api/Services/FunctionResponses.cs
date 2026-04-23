using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace TheLibrary.Api.Services;

public static class FunctionResponses
{
    public static async Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode status, string message)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }

    public static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode status, T payload)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(payload);
        return response;
    }
}