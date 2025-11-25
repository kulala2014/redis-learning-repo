# 4. Redis 进阶模式

## 1. 发布/订阅 (Pub/Sub)

Redis 可以作为轻量级的消息总线。

*   **Publish**: 发送消息到频道。
*   **Subscribe**: 监听频道消息。

**注意**：Redis 的 Pub/Sub 是“即发即弃”的。如果消费者不在线，消息会丢失。如果需要持久化消息队列，请使用 Redis Streams 或 RabbitMQ/Kafka。

```csharp
// 订阅者
var sub = redis.GetSubscriber();
await sub.SubscribeAsync("news_channel", (channel, message) => {
    Console.WriteLine($"Received: {message}");
});

// 发布者
var pub = redis.GetSubscriber();
await pub.PublishAsync("news_channel", "Breaking News!");
```

## 2. 事务 (Transactions)

Redis 事务通过 `MULTI`, `EXEC` 保证一组命令的原子性执行。在 StackExchange.Redis 中，使用 `ITransaction` 接口。

```csharp
var tran = db.CreateTransaction();

// 所有的命令都是 "Fire and Forget" 或者是 Task，直到 Execute 被调用
tran.StringSetAsync("key1", "value1");
tran.StringSetAsync("key2", "value2");

// 执行事务
bool committed = await tran.ExecuteAsync();
```

## 3. 分布式锁 (Distributed Lock)

在分布式系统中，为了防止多个进程同时操作共享资源，需要使用分布式锁。

Redis 实现锁的基本原理是 `SET key value NX PX expiration` (如果不存在则设置，并设置过期时间)。

StackExchange.Redis 提供了便捷方法：

```csharp
RedisValue token = Environment.MachineName;
string lockKey = "resource_lock";
TimeSpan expiry = TimeSpan.FromSeconds(30);

// 尝试获取锁
if (await db.LockTakeAsync(lockKey, token, expiry))
{
    try
    {
        // 执行临界区代码
        Console.WriteLine("Working...");
    }
    finally
    {
        // 释放锁
        await db.LockReleaseAsync(lockKey, token);
    }
}
else
{
    Console.WriteLine("Could not acquire lock.");
}
```

### 3.1 为什么需要 RedLock？

普通的 `SETNX` 分布式锁在 **Redis 集群 (Cluster)** 或 **主从 (Sentinel)** 架构下存在一个极端风险：

1.  客户端 A 在 Master 节点获取了锁。
2.  Master 节点挂了，且**锁数据还没来得及同步到 Slave 节点**。
3.  Slave 节点升级为新的 Master。
4.  客户端 B 在新的 Master 上也获取了同一个锁。
5.  **结果**：A 和 B 同时持有了锁，锁失效。

### 3.2 RedLock 算法原理

RedLock 算法由 Redis 作者 Antirez 提出，核心思想是：**少数服从多数**。

假设你有 5 个完全独立的 Redis Master 节点（注意：不是主从关系，是 5 个独立的 Redis 实例）：

1.  客户端尝试按顺序向这 5 个实例请求加锁。
2.  如果客户端能在**超过半数**（>=3）的实例上成功获取锁。
3.  并且获取锁消耗的总时间小于锁的有效时间。
4.  那么，认为锁获取成功。

### 3.3 在 .NET 中使用 RedLock

不要自己实现 RedLock 算法，建议使用成熟的库 [RedLock.net](https://github.com/samcook/RedLock.net)。

```bash
dotnet add package RedLock.net
```

```csharp
var endPoints = new List<RedLockEndPoint>
{
    new DnsEndPoint("redis1", 6379),
    new DnsEndPoint("redis2", 6379),
    new DnsEndPoint("redis3", 6379)
};
var lockFactory = RedLockFactory.Create(endPoints);

using (var redLock = await lockFactory.CreateLockAsync("resource_key", TimeSpan.FromSeconds(30)))
{
    if (redLock.IsAcquired)
    {
        // 成功获取锁，执行业务
    }
}
```

## 4. Pipelining (管道)


当你需要连续发送多个命令时，等待每个命令的响应（RTT - Round Trip Time）会很慢。管道技术允许你一次发送多个命令，然后一次性接收所有响应。

在 StackExchange.Redis 中，Pipelining 是**自动**的，当你并发使用 `await` 时，库会尝试将它们打包发送。

```csharp
// 这里的操作会并发发送，利用管道特性
var t1 = db.StringSetAsync("a", "1");
var t2 = db.StringSetAsync("b", "2");
var t3 = db.StringGetAsync("a");

await Task.WhenAll(t1, t2, t3);
```

## 5. Redis 知识点：高可用架构

### 主从复制 (Replication)
*   **原理**：一个 Master，多个 Slave。Master 负责写，Slave 负责读（读写分离）。
*   **作用**：数据冗余备份，提升读性能。
*   **局限**：Master 宕机需要人工切换。

### 哨兵模式 (Sentinel)
*   **原理**：在主从复制的基础上，增加一组 Sentinel 进程监控 Redis 实例。
*   **作用**：**自动故障转移 (Failover)**。当 Master 宕机，Sentinel 会自动选举一个新的 Master。
*   **客户端支持**：StackExchange.Redis 支持 Sentinel 连接，会自动更新 Master 地址。

### 集群模式 (Cluster)
*   **原理**：**分片 (Sharding)**。将数据分散到多个节点（Slot 槽位机制，共 16384 个槽）。
*   **作用**：横向扩展 (Scale Out)，突破单机内存限制。
*   **特点**：去中心化，每个节点都保存数据和集群状态。

