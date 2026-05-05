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