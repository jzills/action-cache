using ActionCache.Common;
using ActionCache.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheOptionsBuilderTests
{
    private ActionCacheOptionsBuilder _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new ActionCacheOptionsBuilder();

    [Test]
    public void UseEntryOptions_Always_ConfiguresEntryOptionsAndReturnsBuilder()
    {
        var returned = _sut.UseEntryOptions(options => options.AbsoluteExpiration = TimeSpan.FromMinutes(5));

        var built = _sut.Build();
        returned.Should().BeSameAs(_sut);
        built.EntryOptions.AbsoluteExpiration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Test]
    public void UseMemoryCache_RegistersABackendWithoutNamingItOnTheOptions()
    {
        // The options object no longer carries per-backend configure delegates: that
        // coupling is what made one package depend on Redis, SQL Server and Cosmos.
        // A backend now contributes an opaque registration instead.
        var returned = _sut.UseMemoryCache(options => options.SizeLimit = 100);

        returned.Should().BeSameAs(_sut);
        _sut.Build().BackendRegistrations.Should().ContainSingle();
    }

    [Test]
    public void UseRedisCache_RegistersABackendAndSuppliesADistributedLocker()
    {
        var returned = _sut.UseRedisCache("localhost:6379");

        returned.Should().BeSameAs(_sut);

        var built = _sut.Build();
        built.BackendRegistrations.Should().ContainSingle();
        built.DistributedLockerFactory.Should().NotBeNull(
            "Redis supports distributed single-flight, so it must supply the locker");
    }

    [Test]
    public void UseAzureCosmosCache_RegistersABackendButSuppliesNoLocker()
    {
        _sut.UseAzureCosmosCache(_ => { });

        var built = _sut.Build();
        built.BackendRegistrations.Should().ContainSingle();
        built.DistributedLockerFactory.Should().BeNull("Cosmos offers no distributed lock");
    }

    [Test]
    public void UseSeveralBackends_RegistersEachOfThem()
    {
        _sut.UseMemoryCache(_ => { }).UseRedisCache("localhost:6379");

        _sut.Build().BackendRegistrations.Should().HaveCount(2);
    }

    [Test]
    public void AddBackend_RunsTheRegistrationAgainstTheServiceCollection()
    {
        var services = new ServiceCollection();
        _sut.AddBackend(collection => collection.AddSingleton("marker"));

        foreach (var register in _sut.Build().BackendRegistrations)
        {
            register(services);
        }

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(string));
    }

    [Test]
    public void FailClosed_Always_SetsFailClosedAndReturnsBuilder()
    {
        var returned = _sut.FailClosed();

        returned.Should().BeSameAs(_sut);
        _sut.Build().FailClosed.Should().BeTrue();
    }

    [Test]
    public void UseOperationTimeout_Always_SetsTheTimeoutAndReturnsBuilder()
    {
        var returned = _sut.UseOperationTimeout(TimeSpan.FromMilliseconds(250));

        returned.Should().BeSameAs(_sut);
        _sut.Build().OperationTimeout.Should().Be(TimeSpan.FromMilliseconds(250));
    }
}
