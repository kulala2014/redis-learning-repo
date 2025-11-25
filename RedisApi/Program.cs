using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 1. 注册 ConnectionMultiplexer (用于直接操作 Redis)
// 建议作为 Singleton 注册
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379", true);
    return ConnectionMultiplexer.Connect(configuration);
});

// 2. 注册 IDistributedCache (用于分布式缓存抽象)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "RedisApi_"; // Key 前缀
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// 演示 1: 使用 IDistributedCache 进行缓存
app.MapGet("/weatherforecast", async (IDistributedCache cache) =>
{
    string cacheKey = "weather_forecast";
    
    // 尝试从缓存获取
    string? cachedWeather = await cache.GetStringAsync(cacheKey);
    
    if (!string.IsNullOrEmpty(cachedWeather))
    {
        Console.WriteLine("Returning from cache");
        return JsonSerializer.Deserialize<WeatherForecast[]>(cachedWeather);
    }

    // 模拟耗时操作
    await Task.Delay(1000); 
    Console.WriteLine("Fetching from source");

    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    // 存入缓存
    var cacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10) // 10秒过期
    };
    
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(forecast), cacheOptions);

    return forecast;
})
.WithName("GetWeatherForecast");

// 演示 2: 直接使用 IConnectionMultiplexer (更高级的操作)
app.MapPost("/api/values", async (IConnectionMultiplexer redis, string key, string value) =>
{
    var db = redis.GetDatabase();
    await db.StringSetAsync(key, value);
    return Results.Ok($"Set {key} = {value}");
});

app.MapGet("/api/values/{key}", async (IConnectionMultiplexer redis, string key) =>
{
    var db = redis.GetDatabase();
    string? value = await db.StringGetAsync(key);
    return value is not null ? Results.Ok(value) : Results.NotFound();
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

