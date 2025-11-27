using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using RedisApi.Models;
using System.Text.Json;

namespace RedisApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly IDistributedCache _cache;
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public WeatherForecastController(IDistributedCache cache)
    {
        _cache = cache;
    }

    [HttpGet]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        string cacheKey = "weather_forecast";
        
        // 尝试从缓存获取
        string? cachedWeather = await _cache.GetStringAsync(cacheKey);
        
        if (!string.IsNullOrEmpty(cachedWeather))
        {
            Console.WriteLine("Returning from cache");
            return JsonSerializer.Deserialize<WeatherForecast[]>(cachedWeather)!;
        }

        // 模拟耗时操作
        await Task.Delay(1000); 
        Console.WriteLine("Fetching from source");

        var forecast = Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();

        // 存入缓存
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10) // 10秒过期
        };
        
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(forecast), cacheOptions);

        return forecast;
    }
}
