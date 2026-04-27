using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc;

namespace Unit.Common.Extensions;

[TestFixture]
public class StatusCodeResultExtensionsTests
{
    [TestCase(200)]
    [TestCase(201)]
    [TestCase(204)]
    [TestCase(226)]
    public void IsSuccessStatusCode_WhenStatusInSuccessRange_ReturnsTrue(int statusCode)
    {
        var result = new StatusCodeResult(statusCode);

        result.IsSuccessStatusCode().Should().BeTrue();
    }

    [TestCase(301)]
    [TestCase(400)]
    [TestCase(404)]
    [TestCase(500)]
    public void IsSuccessStatusCode_WhenStatusOutsideSuccessRange_ReturnsFalse(int statusCode)
    {
        var result = new StatusCodeResult(statusCode);

        result.IsSuccessStatusCode().Should().BeFalse();
    }

    [Test]
    public void IsSuccessStatusCode_WhenOkResult_ReturnsTrue()
    {
        var result = new OkResult();

        result.IsSuccessStatusCode().Should().BeTrue();
    }

    [Test]
    public void IsSuccessStatusCode_WhenNotFoundResult_ReturnsFalse()
    {
        var result = new NotFoundResult();

        result.IsSuccessStatusCode().Should().BeFalse();
    }
}
