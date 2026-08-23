using System.Text.Json;
using ActionCache.Common.Responses;
using ActionCache.Common.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Unit.Common.Responses;

[TestFixture]
public class CachedResponseFactoryTests
{
    private sealed record Forecast(string Summary, int TemperatureC);

    private static CachedResponseFactory Create(JsonSerializerOptions? options = null) =>
        new(options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Test]
    public void TryCreate_FromObjectResult_RendersTheBodyWithTheApplicationsJsonOptions()
    {
        // Proves the app's options are honored rather than the serializer's defaults:
        // with a camelCase policy the body must not contain PascalCase names.
        var factory = Create();

        var created = factory.TryCreate(
            new OkObjectResult(new Forecast("Sunny", 21)),
            request: null,
            variesByRequest: false,
            out var cached);

        created.Should().BeTrue();
        cached!.StatusCode.Should().Be(StatusCodes.Status200OK);
        cached.ContentType.Should().Be("application/json");
        cached.Body.Should().Be("""{"summary":"Sunny","temperatureC":21}""");
    }

    [Test]
    public void TryCreate_FromStatusCodeResult_RecordsTheStatusWithNoBody()
    {
        var created = Create().TryCreate(new NoContentResult(), null, false, out var cached);

        created.Should().BeTrue();
        cached!.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        cached.Body.Should().BeNull();
        cached.ContentType.Should().BeNull();
    }

    [Test]
    public void TryCreate_FromContentResult_KeepsTheContentVerbatim()
    {
        var created = Create().TryCreate(
            new ContentResult { Content = "<p>hi</p>", ContentType = "text/html", StatusCode = 200 },
            null, false, out var cached);

        created.Should().BeTrue();
        cached!.Body.Should().Be("<p>hi</p>");
        cached.ContentType.Should().Be("text/html");
    }

    [Test]
    public void TryCreate_FromAFileResult_IsNotCacheable()
    {
        // A file result does its work at execution time. The previous implementation
        // "cached" it by type name and rebuilt an object holding a disposed stream.
        var result = new FileStreamResult(new MemoryStream([1, 2, 3]), "application/octet-stream");

        var created = Create().TryCreate(result, null, false, out var cached);

        created.Should().BeFalse();
        cached.Should().BeNull();
    }

    [Test]
    public void ToActionResult_ReproducesStatusContentTypeAndBody()
    {
        var cached = new CachedResponse
        {
            StatusCode = 201,
            ContentType = "application/json",
            Body = """{"id":1}"""
        };

        var result = CachedResponseFactory.ToActionResult(cached) as ContentResult;

        result!.StatusCode.Should().Be(201);
        result.ContentType.Should().Be("application/json");
        result.Content.Should().Be("""{"id":1}""");
    }

    [Test]
    public void TryCreateFromEndpointResult_ForAValueResult_RendersTheBody()
    {
        var created = Create().TryCreateFromEndpointResult(
            Results.Ok(new Forecast("Rainy", 9)), null, false, out var cached);

        created.Should().BeTrue();
        cached!.Body.Should().Be("""{"summary":"Rainy","temperatureC":9}""");
    }

    [Test]
    public void TryCreateFromEndpointResult_ForAPlainObject_RendersTheBody()
    {
        var created = Create().TryCreateFromEndpointResult(
            new Forecast("Cloudy", 14), null, false, out var cached);

        created.Should().BeTrue();
        cached!.StatusCode.Should().Be(StatusCodes.Status200OK);
        cached.Body.Should().Be("""{"summary":"Cloudy","temperatureC":14}""");
    }

    [Test]
    public void SerializedEnvelope_ContainsNoTypeDiscriminator()
    {
        // The regression guarding the removal of TypeNameHandling: nothing in a cached
        // payload may name a type for deserialization to construct.
        Create().TryCreate(new OkObjectResult(new Forecast("Sunny", 21)), null, false, out var cached);

        var json = CacheJsonSerializer.Serialize(cached);

        json.Should().NotContain("$type");
        json.Should().NotContain("ActionCache");
        json.Should().NotContain("System.Private.CoreLib");
    }

    [Test]
    public void CachedResponse_RoundTripsThroughTheCacheSerializer()
    {
        var original = new CachedResponse
        {
            StatusCode = 200,
            ContentType = "application/json",
            Body = """{"a":1}""",
            VariesByRequest = true,
            Request = new CachedRequest { Method = "GET", Path = "/users/me", QueryString = "?page=1" }
        };

        var round = CacheJsonSerializer.Deserialize<CachedResponse>(CacheJsonSerializer.Serialize(original));

        round.Should().BeEquivalentTo(original);
    }

    [Test]
    public void CreateRequest_RecordsMethodPathAndQuery()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/users/me";
        httpContext.Request.QueryString = new QueryString("?page=2");

        var request = CachedResponseFactory.CreateRequest(httpContext);

        request.Method.Should().Be("GET");
        request.Path.Should().Be("/users/me");
        request.QueryString.Should().Be("?page=2");
    }
}
