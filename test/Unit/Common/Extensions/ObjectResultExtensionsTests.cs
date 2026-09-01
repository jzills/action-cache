using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Unit.Common.Extensions;

[TestFixture]
public class ObjectResultExtensionsTests
{
    [TestCase(200)]
    [TestCase(201)]
    [TestCase(204)]
    [TestCase(226)]
    public void IsSuccessStatusCode_WhenStatusInSuccessRange_ReturnsTrue(int statusCode)
    {
        var result = new ObjectResult("data") { StatusCode = statusCode };

        result.IsSuccessStatusCode().Should().BeTrue();
    }

    [TestCase(400)]
    [TestCase(404)]
    [TestCase(500)]
    [TestCase(301)]
    public void IsSuccessStatusCode_WhenStatusOutsideSuccessRange_ReturnsFalse(int statusCode)
    {
        var result = new ObjectResult("data") { StatusCode = statusCode };

        result.IsSuccessStatusCode().Should().BeFalse();
    }

    [Test]
    public void IsSuccessStatusCode_WhenStatusCodeIsNull_ReturnsTrue()
    {
        // A null StatusCode means the framework will serialize the result as 200.
        var result = new ObjectResult("data") { StatusCode = null };

        result.IsSuccessStatusCode().Should().BeTrue();
    }

    [Test]
    public void IsSuccessStatusCode_WhenOkObjectResult_ReturnsTrue()
    {
        var result = new OkObjectResult("data");

        result.IsSuccessStatusCode().Should().BeTrue();
    }

    [Test]
    public void IsSuccessStatusCode_WhenBadRequestObjectResult_ReturnsFalse()
    {
        var result = new BadRequestObjectResult("error");

        result.IsSuccessStatusCode().Should().BeFalse();
    }
}
