using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheLibrary.Api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
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
