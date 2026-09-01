using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc;

namespace Unit.Common.Extensions;

[TestFixture]
public class IActionResultExtensionsTests
{
    [Test]
    public void IsSuccessfulResult_WhenOkObjectResult_ReturnsTrue()
    {
        IActionResult result = new OkObjectResult("data");

        result.IsSuccessfulResult().Should().BeTrue();
    }

    [Test]
    public void IsSuccessfulResult_WhenOkResult_ReturnsTrue()
    {
        IActionResult result = new OkResult();

        result.IsSuccessfulResult().Should().BeTrue();
    }

    [Test]
    public void IsSuccessfulResult_WhenBadRequestObjectResult_ReturnsFalse()
    {
        IActionResult result = new BadRequestObjectResult("error");

        result.IsSuccessfulResult().Should().BeFalse();
    }

    [Test]
    public void IsSuccessfulResult_WhenNotFoundResult_ReturnsFalse()
    {
        IActionResult result = new NotFoundResult();

        result.IsSuccessfulResult().Should().BeFalse();
    }

    [Test]
    public void IsSuccessfulObjectResult_WhenOkObjectResult_ReturnsTrue()
    {
        IActionResult result = new OkObjectResult("data");

        result.IsSuccessfulObjectResult().Should().BeTrue();
    }

    [Test]
    public void IsSuccessfulObjectResult_WhenStatusCodeResult_ReturnsFalse()
    {
        IActionResult result = new OkResult();

        result.IsSuccessfulObjectResult().Should().BeFalse();
    }

    [Test]
    public void IsSuccessfulStatusCodeResult_WhenOkResult_ReturnsTrue()
    {
        IActionResult result = new OkResult();

        result.IsSuccessfulStatusCodeResult().Should().BeTrue();
    }

    [Test]
    public void IsSuccessfulStatusCodeResult_WhenObjectResult_ReturnsFalse()
    {
        IActionResult result = new OkObjectResult("data");

        result.IsSuccessfulStatusCodeResult().Should().BeFalse();
    }

    [TestCase(201)]
    [TestCase(204)]
    [TestCase(226)]
    public void IsSuccessfulResult_WhenSuccessStatusCodeResult_ReturnsTrue(int statusCode)
    {
        IActionResult result = new StatusCodeResult(statusCode);

        result.IsSuccessfulResult().Should().BeTrue();
    }

    [TestCase(400)]
    [TestCase(404)]
    [TestCase(500)]
    public void IsSuccessfulResult_WhenErrorStatusCodeResult_ReturnsFalse(int statusCode)
    {
        IActionResult result = new StatusCodeResult(statusCode);

        result.IsSuccessfulResult().Should().BeFalse();
    }
}
