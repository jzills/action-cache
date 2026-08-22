using ActionCache.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Integration.Controllers;

[Route("[controller]")]
public class UsersController : Controller
{
    [HttpGet("")]
    [ActionCache(Namespace = "Users")]
    public IActionResult Get() =>
        Ok(new object[]
        {
            new { Id = 1, Name = "Joshua" },
            new { Id = 2, Name = "Sam" },
            new { Id = 3, Name = "Izzy" },
            new { Id = 4, Name = "Vanessa" }
        });

    [HttpPost("query")]
    [ActionCache(Namespace = "Users")]
    public IActionResult GetWithQuery([FromBody] Query query) =>
        Ok(new object[]
        {
            new { Id = 1, Name = "Joshua" },
            new { Id = 2, Name = "Sam" },
            new { Id = 3, Name = "Izzy" },
            new { Id = 4, Name = "Vanessa" }
        });

    /// <summary>
    /// Counts how many times <see cref="GetSingleFlight"/> actually ran, so a test can
    /// assert that concurrent requests coalesced onto one execution.
    /// </summary>
    public static int SingleFlightInvocations;

    [HttpGet("single-flight")]
    [ActionCache(Namespace = "SingleFlight")]
    public async Task<IActionResult> GetSingleFlight()
    {
        Interlocked.Increment(ref SingleFlightInvocations);

        // A little latency widens the window every concurrent request would otherwise
        // race through, which is what makes the stampede observable.
        await Task.Delay(50);

        return Ok(new { Value = "single-flight" });
    }

    /// <summary>
    /// Source data a refresh test mutates between requests, so a stale cache entry and a
    /// refreshed one are distinguishable.
    /// </summary>
    public static string RefreshableValue = "original";

    [HttpGet("refreshable")]
    [ActionCache(Namespace = "Replay")]
    public IActionResult GetRefreshable() => Ok(new { Value = RefreshableValue });

    [HttpPost("refreshable")]
    [ActionCacheRefresh(Namespace = "Replay")]
    public IActionResult RefreshRefreshable() => Ok();

    [HttpGet("me")]
    [ActionCache(Namespace = "VaryByUser")]
    public IActionResult GetMe() =>
        Ok(new { Name = User.Identity?.Name ?? "anonymous" });

    /// <summary>
    /// A body-bearing cached action whose response tracks mutable source data, so a test
    /// can tell a genuinely refreshed entry from one that was merely left alone.
    /// </summary>
    public static string RefreshableBodyValue = "original";

    [HttpPost("query-refreshable")]
    [ActionCache(Namespace = "BodyReplay")]
    public IActionResult GetRefreshableWithBody([FromBody] Query query) =>
        Ok(new { Value = RefreshableBodyValue, ShowAll = query.ShowAll });

    [HttpPost("query-refreshable/refresh")]
    [ActionCacheRefresh(Namespace = "BodyReplay")]
    public IActionResult RefreshBodyReplay() => Ok();

    [HttpPost("")]
    [ActionCacheRefresh(Namespace = "Users")]
    public IActionResult Post() => Ok();

    [HttpDelete("")]
    [ActionCacheEviction(Namespace = "Users")]
    public IActionResult Delete() => Ok();
}

public class Query
{
    public Guid[]? IncludeIds { get; set; }
    public bool ShowAll { get; set; }
    public SubQuery[]? SubQueries { get; set; }
}

public class SubQuery
{
    public string? Contains { get; set; }
}

public class User
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public ContactInfo? ContactInfo { get; set; }
}

public class ContactInfo
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}