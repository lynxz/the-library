using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheLibrary.Api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var maxRequestBodyBytes = 60L * 1024 * 1024;
        if (long.TryParse(context.Configuration["MaxRequestBodySizeBytes"], out var configuredLimit) && configuredLimit > 0)
        {
            maxRequestBodyBytes = configuredLimit;
        }

        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
        });

        services.AddAzureClients(clientBuilder =>
        {
            var connectionString = context.Configuration["StorageConnectionString"]
                ?? throw new InvalidOperationException("StorageConnectionString is not configured.");

            clientBuilder.AddTableServiceClient(connectionString);
            clientBuilder.AddBlobServiceClient(connectionString);
        });

        var jwtKey = context.Configuration["JwtSigningKey"]
            ?? throw new InvalidOperationException("JwtSigningKey is not configured.");
        services.AddSingleton(new JwtHelper(jwtKey));
    })
    .Build();

host.Run();
