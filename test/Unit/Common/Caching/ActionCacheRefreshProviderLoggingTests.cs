using ActionCache.Common.Caching;
using ActionCache.Common.Keys;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Unit.TestUtilities;

namespace Unit.Common.Caching;

/// <summary>
/// Locks in the refresh diagnostics: per-key skip reasons, the accurate requested/refreshed
/// summary counts, and the warning raised when no controller actions match the namespace.
/// </summary>
[TestFixture]
public class ActionCacheRefreshProviderLoggingTests
{
    private Mock<IActionCacheDescriptorProvider> _descriptorProviderMock;
    private CapturingLogger<ActionCacheRefreshProvider> _logger;
    private ActionCacheRefreshProvider _sut;

    [SetUp]
    public void SetUp()
    {
        _descriptorProviderMock = new Mock<IActionCacheDescriptorProvider>();
        _logger = new CapturingLogger<ActionCacheRefreshProvider>();
        _sut = new ActionCacheRefreshProvider(_descriptorProviderMock.Object, _logger);
    }

    [Test]
    public void GetRefreshResults_WhenNoActionsMatchNamespace_WarnsAndReportsRequestedCount()
    {
        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(new ActionCacheDescriptor());

        _sut.GetRefreshResults(new Namespace("Test"), ["key-one", "key-two"]);

        var warning = _logger.Entries.Should().ContainSingle(entry => entry.EventId.Id == 3002).Subject;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Message.Should().Contain("no matching controller actions").And.Contain("2");

        var summary = _logger.Entries.Should().ContainSingle(entry => entry.EventId.Id == 3001).Subject;
        summary.Message.Should().Contain("0 of 2");
    }

    [Test]
    public void GetRefreshResults_WhenNoKeysRequested_DoesNotWarn()
    {
        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(new ActionCacheDescriptor());

        _sut.GetRefreshResults(new Namespace("Test"), []);

        _logger.Entries.Should().NotContain(entry => entry.EventId.Id == 3002);
        _logger.Entries.Should().ContainSingle(entry => entry.EventId.Id == 3001)
            .Which.Message.Should().Contain("0 of 0");
    }

    [Test]
    public void GetRefreshResults_WhenKeyDoesNotMatchAnAction_LogsSkipReasonAndSummary()
    {
        var controller = new RefreshLoggingTestController();
        var methodInfo = typeof(RefreshLoggingTestController).GetMethod(nameof(RefreshLoggingTestController.GetValue))!;

        var descriptor = new ActionCacheDescriptor();
        descriptor.Add("OtherController:OtherAction", methodInfo, controller);

        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(descriptor);
        _descriptorProviderMock
            .Setup(provider => provider.CreateKey(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("TestController:GetValue");

        var encodedKey = new ActionCacheKeyBuilder()
            .WithRouteValues(new RouteValueDictionary
            {
                { "controller", "TestController" },
                { "action", "GetValue" }
            })
            .Build();

        _sut.GetRefreshResults(new Namespace("Test"), [encodedKey]);

        var skipped = _logger.Entries.Should().ContainSingle(entry => entry.EventId.Id == 3000).Subject;
        skipped.Level.Should().Be(LogLevel.Debug);
        skipped.Message.Should().Contain("no matching action method was found");

        _logger.Entries.Should().ContainSingle(entry => entry.EventId.Id == 3001)
            .Which.Message.Should().Contain("0 of 1");
    }
}

file class RefreshLoggingTestController
{
    public string GetValue() => "cached-value";
}
