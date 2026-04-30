using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Http;

namespace Unit.Common.Extensions;

[TestFixture]
public class HttpResponseExtensionsTests
{
    private HttpResponse CreateResponse(int statusCode)
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = statusCode;
        return context.Response;
    }

    [TestCase(200)]
    [TestCase(201)]
    [TestCase(204)]
    [TestCase(226)]
    public void IsSuccessStatusCode_WhenSuccessRange_ReturnsTrue(int statusCode)
    {
        var response = CreateResponse(statusCode);

        response.IsSuccessStatusCode().Should().BeTrue();
    }

    [TestCase(100)]
    [TestCase(199)]
    [TestCase(301)]
    [TestCase(400)]
    [TestCase(404)]
    [TestCase(500)]
    public void IsSuccessStatusCode_WhenOutsideSuccessRange_ReturnsFalse(int statusCode)
    {
        var response = CreateResponse(statusCode);

        response.IsSuccessStatusCode().Should().BeFalse();
    }
}
