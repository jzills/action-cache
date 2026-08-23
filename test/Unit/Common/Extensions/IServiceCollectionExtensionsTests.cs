using ActionCache;
using ActionCache.AzureCosmos.Exceptions;
using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Extensions;
using ActionCache.Common.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Common.Extensions;

[TestFixture]
public class IServiceCollectionExtensionsTests
{
    [Test]
    public void AddActionCache_WithMemoryCache_RegistersIActionCacheFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFactory));
    }

    [Test]
    public void AddActionCache_WithMemoryCache_RegistersFilterAbstractFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFilterAbstractFactory<IFilterMetadata>));
    }

    [Test]
    public void AddActionCache_WithMemoryCache_RegistersEndpointFilterAbstractFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFilterAbstractFactory<IEndpointFilter>));
    }

    [Test]
    public void AddActionCache_WithMemoryCache_RegistersRefreshProvider()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheRefreshProvider));
    }

    [Test]
    public void AddActionCache_WithEntryOptions_ConfiguresOptions()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options =>
        {
            options.UseMemoryCache(opt => { });
            options.UseEntryOptions(entryOptions =>
            {
                entryOptions.AbsoluteExpiration = TimeSpan.FromMinutes(5);
                entryOptions.SlidingExpiration = TimeSpan.FromMinutes(1);
            });
        });

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFactory));
    }

    [Test]
    public void AddActionCache_NoBackend_ReturnsServices()
    {
        var services = new ServiceCollection();

        var result = services.AddActionCache(options => { });

        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddActionCache_WithSqlServerCache_RegistersIActionCacheFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseSqlServerCache(sqlOptions =>
        {
            sqlOptions.ConnectionString = "Server=localhost;Database=Cache;Trusted_Connection=True";
            sqlOptions.SchemaName = "dbo";
            sqlOptions.TableName = "CacheEntries";
        }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFactory));
    }

    [Test]
    public void AddActionCache_WithAzureCosmosCache_WhenConnectionStringMissing_ThrowsMissingConnectionStringException()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddActionCache(options =>
            options.UseAzureCosmosCache(cosmosOptions => { }));

        act.Should().Throw<MissingConnectionStringException>();
    }

    [Test]
    public void AddActionCache_WithAzureCosmosCache_WhenDatabaseIdMissing_ThrowsMissingDatabaseIdException()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddActionCache(options =>
            options.UseAzureCosmosCache(cosmosOptions =>
            {
                cosmosOptions.ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";
            }));

        act.Should().Throw<MissingDatabaseIdException>();
    }

    [Test]
    public void AddActionCache_WhenDistributedSingleFlightHasNoBackend_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();

        var register = () => services.AddActionCache(options => options
            .UseMemoryCache(_ => { })
            .UseDistributedSingleFlight());

        register.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a Redis or SQL Server cache backend*");
    }
}
