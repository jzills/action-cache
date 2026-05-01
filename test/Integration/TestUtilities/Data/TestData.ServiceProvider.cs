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
                GetMultipleCacheServiceProvider());

    public static IEnumerable<IServiceProvider> GetRedisCacheServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMvc();
        services.AddActionCache(options =>
        {
            options.UseEntryOptions(entryOptions => { });
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
            options.UseEntryOptions(entryOptions => { });
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
            options.UseEntryOptions(entryOptions => { });
            options.UseAzureCosmosCache(options =>
            {
                options.DatabaseId = "ActionCache";
                options.ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b5seMGOPXxiI3g5MVGR8";
                options.CosmosClientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
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
            options.UseEntryOptions(entryOptions => { });
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
