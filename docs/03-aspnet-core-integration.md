# ASP.NET Core Redis 集成指南：从入门到精通

本文档旨在帮助开发者循序渐进地掌握在 ASP.NET Core 中使用 Redis 的各种技巧，从最基础的配置到高阶的性能优化。

## Level 1: 基础接入 (Basics)

### 1.1 依赖注入配置

在 `Program.cs` 中注册服务是使用 Redis 的第一步。

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

### 1.2 IDistributedCache vs IConnectionMultiplexer

在开始之前，了解这两个核心接口的区别至关重要。

| 特性 | IDistributedCache | IConnectionMultiplexer |
| :--- | :--- | :--- |
| **抽象级别** | 高 (通用接口) | 低 (Redis 原生) |
| **支持操作** | 仅 String (Get/Set/Refresh/Remove) | 所有 Redis 命令 (List, Hash, Set...) |
| **切换成本** | 低 (可换成 Memory, SQL) | 高 (绑定 Redis) |
| **适用场景** | 简单的页面/数据缓存 | 复杂业务逻辑、计数器、排行榜、锁 |

---

## Level 2: 标准用法 (Standard Usage)

### 2.1 Cache-Aside 模式 (旁路缓存)

这是最常用的缓存策略，适用于大多数读多写少的场景。

**流程**：
1.  应用程序先查询缓存。
2.  如果缓存命中 (Hit)，直接返回数据。
3.  如果缓存未命中 (Miss)，查询数据库。
4.  将数据库结果写入缓存，并设置过期时间。
5.  返回数据。

**代码示例**：

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

### 2.2 Redis 数据结构应用场景示例

除了基本的 Key-Value 缓存，Redis 丰富的数据结构能解决很多特定的业务问题。

#### String (字符串)
*   **场景**：高并发计数器（如文章阅读量、视频播放数）。
*   **优势**：`INCR` 是原子操作，线程安全且性能极高。
    ```csharp
    await db.StringIncrementAsync($"article:{id}:views");
    ```

#### Hash (哈希)
*   **场景**：存储对象（如购物车、用户资料）。
*   **优势**：比存储整个 JSON 字符串更灵活，支持单独读取/修改某个字段，节省网络流量。
    ```csharp
    // 添加商品到购物车
    await db.HashIncrementAsync($"cart:{userId}", productId, quantity);
    // 获取购物车所有商品
    await db.HashGetAllAsync($"cart:{userId}");
    ```

#### List (列表)
*   **场景**：简单的消息队列、最新消息流 (Timeline)。
*   **优势**：有序，支持阻塞读取 (`BLPOP`/`BRPOP`) 实现生产者-消费者模型。
    ```csharp
    // 入队 (左进)
    await db.ListLeftPushAsync("task_queue", taskJson);
    // 出队 (右出)
    await db.ListRightPopAsync("task_queue");
    ```

#### Set (集合)
*   **场景**：标签系统、抽奖(去重)、共同好友/共同关注。
*   **优势**：自动去重，支持集合运算（交集 `SINTER`、并集 `SUNION`、差集 `SDIFF`）。
    ```csharp
    // 添加兴趣标签
    await db.SetAddAsync($"user:{id}:interests", "coding");
    // 计算两个用户的共同兴趣 (交集)
    await db.SetCombineAsync(SetOperation.Intersect, key1, key2);
    ```

#### Sorted Set (有序集合 ZSet)
*   **场景**：排行榜（游戏排名、热搜榜）、带权重的任务队列。
*   **优势**：元素唯一且带分数 (Score)，天然有序，支持按排名或分数范围查询。
    ```csharp
    // 更新玩家分数
    await db.SortedSetAddAsync("leaderboard", "player1", 100);
    // 获取前 10 名
    await db.SortedSetRangeByRankWithScoresAsync("leaderboard", stop: 9, order: Order.Descending);
    ```

#### Bitmap (位图)
*   **场景**：用户签到、在线状态统计。
*   **优势**：极度节省空间。1 bit 代表一个状态，统计 1 亿用户的在线状态只需约 12MB 内存。
    ```csharp
    // 第 5 天签到
    await db.StringSetBitAsync($"signin:{userId}:{month}", 4, true);
    // 统计当月签到天数
    await db.StringBitCountAsync($"signin:{userId}:{month}");
    ```

#### Geo (地理位置)
*   **场景**：附近的人、附近的店铺、距离计算。
*   **优势**：基于 GeoHash 算法，内置距离计算和半径搜索功能。
    ```csharp
    // 添加店铺位置
    await db.GeoAddAsync("shops", lon, lat, "shop1");
    // 搜索附近 5km 的店铺
    await db.GeoRadiusAsync("shops", userLon, userLat, 5, GeoUnit.Kilometers);
    ```

#### HyperLogLog (基数统计)
*   **场景**：海量数据的 UV (独立访客) 统计。
*   **优势**：占用极小的固定空间 (约 12KB) 即可统计 2^64 个不同元素，误差率仅 0.81%。
    ```csharp
    // 记录访问 IP
    await db.HyperLogLogAddAsync($"uv:{page}", ip);
    // 获取估算的 UV 数
    await db.HyperLogLogLengthAsync($"uv:{page}");
    ```

---

## Level 3: 常见问题与解决方案 (Common Pitfalls)

在生产环境中，简单的缓存模式可能会遇到并发问题。

### 3.1 防止缓存穿透 (Cache Penetration)

**问题**：恶意用户大量请求不存在的 ID (如 -1)，导致请求总是绕过缓存直接查询数据库。

**解决方案**：**缓存空对象 (Null Object Pattern)**。
当数据库查不到数据时，在缓存中写入一个特殊的空值标记（如 `@@NULL@@`），并设置较短的过期时间（如 30秒）。

```csharp
var product = await db.Products.FindAsync(id);
if (product == null)
{
    // 写入空值标记，防止下次再查库
    await cache.SetStringAsync(key, "@@NULL@@", TimeSpan.FromSeconds(30));
    return Results.NotFound();
}
```

### 3.2 防止缓存击穿 (Cache Stampede)

**问题**：当一个热点 Key 过期时，大量并发请求同时发现缓存失效，瞬间全部打到数据库，可能导致数据库宕机。

**解决方案**：**双重检查锁定 (Double-Check Locking)**。
保证同一时间只有一个线程/实例去查询数据库，其他请求等待。

```csharp
// 伪代码示例：使用 RedLock 防止击穿
using (var redLock = await lockFactory.CreateLockAsync(lockKey, ...))
{
    if (redLock.IsAcquired)
    {
        // Double Check: 再次检查缓存
        val = await cache.GetStringAsync(key);
        if (val != null) return val;

        // 查库 & 写缓存
        val = db.Query(...);
        await cache.SetStringAsync(key, val);
    }
}
```

### 3.3 防止缓存雪崩 (Cache Avalanche)

**问题**：大量 Key 在**同一时间过期**，或者 Redis 服务宕机，导致所有请求瞬间打到数据库，造成数据库崩溃。

**解决方案**：
1.  **随机过期时间 (Random Jitter)**：在设置过期时间时，加上一个随机值（例如 1-60秒），让 Key 的过期时间分散开。
    ```csharp
    var baseExpiry = TimeSpan.FromMinutes(5);
    var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 60));
    await cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { 
        AbsoluteExpirationRelativeToNow = baseExpiry + jitter 
    });
    ```
2.  **高可用架构**：使用 Redis Sentinel 或 Cluster。
3.  **限流降级**：当数据库压力过大时，直接返回错误或默认值。

### 3.4 缓存一致性与更新策略

**问题**：当数据更新时，如何保证缓存和数据库的一致性？

**最佳实践**：**Cache Invalidation (删除缓存)**。
更新数据时，**先更新数据库，再删除缓存**。

```csharp
// 更新操作
using (var lock = await lockFactory.CreateLockAsync(...))
{
    // 1. 更新数据库
    await db.SaveChangesAsync();
    
    // 2. 删除 L2 缓存
    await cache.RemoveAsync(key);
    
    // 3. 删除 L1 缓存
    localCache.Remove(key);
}
```

---

## Level 4: 高性能架构 (High Performance)

### 4.1 多级缓存 (L1 Memory + L2 Redis)

**场景**：对于极高并发（如 10万 QPS）或超热点数据，Redis 的网络带宽可能成为瓶颈。

**架构**：
*   **L1 (进程内缓存)**: 使用 `IMemoryCache`。速度极快（纳秒级），无网络开销，但各实例间数据不一致。
*   **L2 (分布式缓存)**: 使用 Redis。速度快（毫秒级），数据一致。

**读取逻辑**：先查 L1 -> Miss -> 查 L2 -> Miss -> 查 DB -> 回填 L2 -> 回填 L1。

### 4.2 Pipeline (管道模式)

**场景**：需要批量执行大量命令（如缓存预热、批量写入）。
**优势**：将多个命令打包发送，大幅减少网络往返 (RTT) 时间。

```csharp
var batch = db.CreateBatch();
for (int i = 0; i < 1000; i++)
{
    // 这些命令不会立即发送
    tasks.Add(batch.StringSetAsync($"key:{i}", i));
}
// 一次性发送
batch.Execute();
await Task.WhenAll(tasks);
```

### 4.3 Lua 脚本 (原子性操作)

**场景**：需要执行多个 Redis 命令，且要求中间不能被插入其他命令（如秒杀扣库存）。
**优势**：
*   **原子性**：Redis 保证脚本执行期间是原子的，无需分布式锁。
*   **减少网络开销**：一次请求完成所有逻辑。

```csharp
// Lua 脚本：检查库存并扣减
string script = @"
    if redis.call('get', KEYS[1]) > 0 then
        return redis.call('decr', KEYS[1])
    else
        return -1
    end";
await db.ScriptEvaluateAsync(script, ...);
```

### 4.4 Big Key (大 Key 问题)

**定义**：Value 占用内存过大（如 String > 10KB，List > 1万个元素）。
**危害**：阻塞主线程、网络阻塞。

**解决方案**：
*   **拆分**：将大 List 拆分成多个小 List。
*   **异步删除 (UNLINK)**：使用 `UNLINK` 命令代替 `DEL`。
    ```csharp
    // StackExchange.Redis 默认 KeyDelete 是 DEL
    // 使用 ExecuteAsync 调用 UNLINK
    await db.ExecuteAsync("UNLINK", "big_key");
    ```

---

## Level 5: 运维与理论 (Ops & Theory)

### 5.1 内存淘汰策略

当 Redis 内存使用达到 `maxmemory` 限制时，需要决定删除哪些数据。

*   **noeviction**：默认策略。不删除任何数据，拒绝写入操作。
*   **allkeys-lru**：在**所有** Key 中，删除最近最少使用 (LRU) 的 Key。**（最常用作缓存）**
*   **volatile-lru**：在**设置了过期时间**的 Key 中，删除最近最少使用的 Key。
*   **allkeys-random**：随机删除 Key。
*   **volatile-ttl**：在设置了过期时间的 Key 中，删除剩余存活时间 (TTL) 最短的 Key。

---

## Level 6: 实战场景：秒杀与分布式锁 (Seckill & Distributed Locks)

秒杀系统是 Redis 最经典的实战场景之一，核心挑战在于**高并发下的库存扣减一致性**。

### 6.1 方案对比

| 方案 | 原理 | 性能 | 适用场景 |
| :--- | :--- | :--- | :--- |
| **Lua 脚本** | 原子执行脚本 (检查+扣减) | **最高** | 追求极致吞吐量，逻辑简单 |
| **Redis 事务** | 乐观锁 (`WATCH` + `MULTI`) | 中等 | 并发不高，冲突较少 |
| **RedLock** | 分布式锁 (互斥访问) | 最低 | 业务逻辑复杂，强一致性要求 |
| **SETNX** | 原生简单锁 | 低 | 单机 Redis，简单互斥 |

### 6.2 方案 A: Lua 脚本 (推荐)

利用 Redis 单线程特性和 Lua 脚本的原子性，将“检查库存”和“扣减库存”合并为一步操作。

```csharp
string script = @"
    local stock = tonumber(redis.call('get', KEYS[1]))
    if stock and stock > 0 then
        redis.call('decr', KEYS[1])
        return stock - 1
    else
        return -1
    end";
var result = (int)await db.ScriptEvaluateAsync(script, new RedisKey[] { "product:stock" });
```

### 6.3 方案 B: Redis 事务 (乐观锁)

使用 `Condition` (对应 Redis 的 `WATCH`) 实现乐观锁。如果事务执行期间 Key 被修改，事务会失败。

```csharp
var trans = db.CreateTransaction();
// 乐观锁：只有当库存值等于我们刚才读到的值时，才执行扣减
trans.AddCondition(Condition.StringEqual("product:stock", currentStockVal));
trans.StringDecrementAsync("product:stock");
bool committed = await trans.ExecuteAsync(); // 如果冲突，返回 false
```

### 6.4 方案 C: RedLock (分布式锁)

使用 `RedLock.net` 库实现强一致性的分布式锁。虽然性能不如 Lua，但最稳健。

```csharp
// 自动处理加锁、续期、释放
using (var redLock = await lockFactory.CreateLockAsync(resource, expiry, wait, retry))
{
    if (redLock.IsAcquired)
    {
        // 查库存 -> 扣库存
        // 此时是串行执行，无需担心并发问题
    }
}
```

### 6.5 方案 D: 原生 SETNX 锁

使用 `SET key value NX PX expiry` 实现简单的互斥锁。

**加锁**：
```csharp
// NX: 不存在才设置; PX: 自动过期
bool locked = await db.StringSetAsync(key, uuid, TimeSpan.FromSeconds(5), When.NotExists);
```

**解锁 (必须用 Lua)**：
防止误删别人的锁（比如自己的锁过期了，别人加了新锁，不能直接 DEL）。
```csharp
string script = @"
    if redis.call('get', KEYS[1]) == ARGV[1] then
        return redis.call('del', KEYS[1])
    else
        return 0
    end";
```

---

## Level 7: 消息与事件 (Messaging & Events)

Redis 不仅仅是缓存，它还是一个轻量级的消息中间件。

### 7.1 Pub/Sub (发布/订阅)

**场景**：实时通知、配置动态刷新、WebSocket 消息推送。
**特点**：消息是**即发即失**的。如果消费者不在线，消息就丢失了。

**发布者 (Publisher)**:
```csharp
var sub = redis.GetSubscriber();
await sub.PublishAsync(RedisChannel.Literal("alerts"), "System is down!");
```

**订阅者 (Subscriber)**:
通常在 `BackgroundService` 中运行。
```csharp
await sub.SubscribeAsync(RedisChannel.Literal("alerts"), (channel, message) =>
{
    Console.WriteLine($"Received: {message}");
});
```

### 7.2 Streams (流数据)

**场景**：持久化消息队列、事件溯源 (Event Sourcing)。
**特点**：Redis 5.0+ 引入。消息可持久化，支持消费者组 (Consumer Groups)，支持 ACK 确认机制。比 List 做队列更可靠。

**生产者 (XADD)**:
```csharp
var db = redis.GetDatabase();
// 自动生成 ID (*)
await db.StreamAddAsync("events", new NameValueEntry[]
{
    new NameValueEntry("user", "u1"),
    new NameValueEntry("action", "login")
});
```

**消费者 (XREAD)**:
```csharp
// 读取最新消息
var events = await db.StreamRangeAsync("events", "-", "+", count: 10);
```

### 7.3 Keyspace Notifications (键空间通知)

**场景**：订单超时自动取消。
**原理**：监听 Key 的过期事件 (`__keyevent@0__:expired`)。
**配置**：需要 Redis 开启 `notify-keyspace-events Ex`。

```csharp
var sub = redis.GetSubscriber();
// 监听所有过期事件
await sub.SubscribeAsync("__keyevent@0__:expired", (channel, key) =>
{
    Console.WriteLine($"Key expired: {key}");
    // 可以在这里触发订单取消逻辑
});
```



