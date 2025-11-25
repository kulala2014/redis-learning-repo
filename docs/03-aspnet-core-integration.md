# 3. ASP.NET Core 集成与缓存策略

在 ASP.NET Core 中使用 Redis 主要有两种方式：直接使用 `IConnectionMultiplexer` 或使用 `IDistributedCache` 抽象。

## 1. 依赖注入配置

在 `Program.cs` 中注册服务：

```csharp
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 方式 A: 注册 IConnectionMultiplexer (推荐用于复杂操作)
// 单例模式，复用连接
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect("localhost:6379");
});

// 方式 B: 注册 IDistributedCache (推荐用于标准缓存)
// 这允许你在未来轻松切换到底层实现（如 SQL Server, Memory）而无需修改业务代码
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "MyApp_"; // 自动为所有 Key 添加前缀
});
```

## 2. Cache-Aside 模式 (旁路缓存)

这是最常用的缓存策略：
1.  应用程序先查询缓存。
2.  如果缓存命中 (Hit)，直接返回数据。
3.  如果缓存未命中 (Miss)，查询数据库。
4.  将数据库结果写入缓存，并设置过期时间。
5.  返回数据。

### 代码示例

```csharp
app.MapGet("/product/{id}", async (IDistributedCache cache, DbContext db, int id) =>
{
    string cacheKey = $"product:{id}";
    
    // 1. 查缓存
    string? cachedData = await cache.GetStringAsync(cacheKey);
    if (!string.IsNullOrEmpty(cachedData))
    {
        return JsonSerializer.Deserialize<Product>(cachedData);
    }

    // 2. 查数据库 (模拟)
    var product = await db.Products.FindAsync(id);
    if (product == null) return Results.NotFound();

    // 3. 写缓存
    var options = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // 10分钟过期
    };
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), options);

    return product;
});
```

## 3. 缓存穿透、击穿与雪崩

在设计缓存系统时需要注意的问题：

*   **缓存穿透**：查询一个**不存在**的数据，缓存不命中，数据库也不命中。导致每次请求都打到数据库。
    *   *解决方案*：缓存空值 (null) 并设置较短过期时间；或使用布隆过滤器。
*   **缓存击穿**：一个**热点 Key** 突然过期，大量并发请求同时打到数据库。
    *   *解决方案*：使用互斥锁 (Mutex/Lock) 保证只有一个线程去查库回写缓存；或设置逻辑过期时间。
*   **缓存雪崩**：大量 Key 在**同一时间过期**，导致数据库压力骤增。
    *   *解决方案*：在过期时间上增加随机值 (Random Jitter)。

## 4. Redis 知识点：内存淘汰策略

当 Redis 内存使用达到 `maxmemory` 限制时，需要决定删除哪些数据。常见的策略有：

*   **noeviction**：默认策略。不删除任何数据，拒绝写入操作，返回错误（只响应读操作）。
*   **allkeys-lru**：在**所有** Key 中，删除最近最少使用 (LRU) 的 Key。**（最常用作缓存）**
*   **volatile-lru**：在**设置了过期时间**的 Key 中，删除最近最少使用的 Key。
*   **allkeys-random**：随机删除 Key。
*   **volatile-ttl**：在设置了过期时间的 Key 中，删除剩余存活时间 (TTL) 最短的 Key。

## 5. IDistributedCache vs IConnectionMultiplexer

| 特性 | IDistributedCache | IConnectionMultiplexer |

| :--- | :--- | :--- |
| **抽象级别** | 高 (通用接口) | 低 (Redis 原生) |
| **支持操作** | 仅 String (Get/Set/Refresh/Remove) | 所有 Redis 命令 (List, Hash, Set...) |
| **切换成本** | 低 (可换成 Memory, SQL) | 高 (绑定 Redis) |
| **适用场景** | 简单的页面/数据缓存 | 复杂业务逻辑、计数器、排行榜、锁 |
