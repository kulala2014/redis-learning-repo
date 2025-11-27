using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RedisApi.Data;
using RedisApi.Models;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers(); // 添加控制器支持

// 0. 注册 SQLite 数据库
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

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

// 3. 注册 MemoryCache (L1 进程内缓存)
builder.Services.AddMemoryCache();

// 4. 注册 RedLockFactory (用于分布式锁)
builder.Services.AddSingleton<RedLockNet.IDistributedLockFactory>(sp =>
{
    var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
    // RedLock 可以连接多个 Redis 实例以提高可靠性
    return RedLockFactory.Create(new[] { new RedLockMultiplexer(multiplexer) });
});

// 5. 注册后台订阅服务
builder.Services.AddHostedService<RedisApi.Services.SubscriberService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 确保数据库创建并应用迁移 (仅用于演示，生产环境请使用迁移工具)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapControllers(); // 映射控制器

app.Run();
