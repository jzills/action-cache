using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.TestUtilities.Data;

public static class TestData
{
    public static IEnumerable<IServiceProvider> GetServiceProviders() =>
        GetRedisCacheServiceProvider().Concat(
            GetSqlServerServiceProvider()).Concat(
                GetAzureCosmosServiceProvider()).Concat(
                    GetMultipleCacheServiceProvider());

    public static IEnumerable<IServiceProvider> GetRedisCacheServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMvc();
        services.AddActionCache(options =>
        {
            options.UseEntryOptions(entryOptions =>
            {
                // These fixtures resolve IActionCache straight from the factory, so they get
                // an *undecorated* cache — unlike the filters, which wrap it in
                // ResilientActionCache. A distributed-lock acquisition that exceeds the
                // timeout therefore throws here instead of degrading, and a loaded CI
                // machine can push SQL Server's sp_getapplock past the 10s default. The
                // generous timeout keeps that infrastructure noise out of the assertions.
                entryOptions.LockTimeout = TimeSpan.FromSeconds(60);
            });
            options.UseRedisCache(options => options.Configuration = "127.0.0.1:6379");
        });

        var server = new TestServer(services.BuildServiceProvider());

        return [server.Services];
    }

    public static IEnumerable<IServiceProvider> GetSqlServerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMvc();
        services.AddActionCache(options =>
        {
            options.UseEntryOptions(entryOptions =>
            {
                // These fixtures resolve IActionCache straight from the factory, so they get
                // an *undecorated* cache — unlike the filters, which wrap it in
                // ResilientActionCache. A distributed-lock acquisition that exceeds the
                // timeout therefore throws here instead of degrading, and a loaded CI
                // machine can push SQL Server's sp_getapplock past the 10s default. The
                // generous timeout keeps that infrastructure noise out of the assertions.
                entryOptions.LockTimeout = TimeSpan.FromSeconds(60);
            });
            options.UseSqlServerCache(options =>
            {
                options.ConnectionString = "Server=localhost;Database=ActionCache;User Id=sa;Password=Password1;Encrypt=True;TrustServerCertificate=True;";
                options.SchemaName = "dbo";
                options.TableName = "DistributedCache";
            });
        });

        var server = new TestServer(services.BuildServiceProvider());

        return [server.Services];
    }

    public static IEnumerable<IServiceProvider> GetAzureCosmosServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMvc();
        services.AddActionCache(options =>
        {
            options.UseEntryOptions(entryOptions =>
            {
                // These fixtures resolve IActionCache straight from the factory, so they get
                // an *undecorated* cache — unlike the filters, which wrap it in
                // ResilientActionCache. A distributed-lock acquisition that exceeds the
                // timeout therefore throws here instead of degrading, and a loaded CI
                // machine can push SQL Server's sp_getapplock past the 10s default. The
                // generous timeout keeps that infrastructure noise out of the assertions.
                entryOptions.LockTimeout = TimeSpan.FromSeconds(60);
            });
            options.UseAzureCosmosCache(options =>
            {
                options.DatabaseId = "ActionCache";
                options.ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                options.CosmosClientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    HttpClientFactory = () => new HttpClient(new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    })
                };
            });
        });

        var server = new TestServer(services.BuildServiceProvider());

        return [server.Services];
    }

    public static IEnumerable<IServiceProvider> GetMultipleCacheServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMvc();
        services.AddActionCache(options =>
        {
            options.UseEntryOptions(entryOptions =>
            {
                // These fixtures resolve IActionCache straight from the factory, so they get
                // an *undecorated* cache — unlike the filters, which wrap it in
                // ResilientActionCache. A distributed-lock acquisition that exceeds the
                // timeout therefore throws here instead of degrading, and a loaded CI
                // machine can push SQL Server's sp_getapplock past the 10s default. The
                // generous timeout keeps that infrastructure noise out of the assertions.
                entryOptions.LockTimeout = TimeSpan.FromSeconds(60);
            });
            options.UseMemoryCache(options => options.SizeLimit = 1000);
            options.UseRedisCache(options => options.Configuration = "127.0.0.1:6379");
            options.UseSqlServerCache(options =>
            {
                options.ConnectionString = "Server=localhost;Database=ActionCache;User Id=sa;Password=Password1;Encrypt=True;TrustServerCertificate=True;";
                options.SchemaName = "dbo";
                options.TableName = "DistributedCache";
            });
        });

        var server = new TestServer(services.BuildServiceProvider());

        return [server.Services];
    }
}
