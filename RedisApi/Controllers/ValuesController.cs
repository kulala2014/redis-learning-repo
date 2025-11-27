using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace RedisApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValuesController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;

    public ValuesController(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    [HttpPost]
    public async Task<IActionResult> Post(string key, string value)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, value);
        return Ok($"Set {key} = {value}");
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var db = _redis.GetDatabase();
        string? value = await db.StringGetAsync(key);
        return value is not null ? Ok(value) : NotFound();
    }
}
