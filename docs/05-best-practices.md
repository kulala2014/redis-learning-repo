# 5. 最佳实践与性能优化

## 1. 连接管理

*   **单例模式**：`ConnectionMultiplexer` 是线程安全的，且创建成本高。**切勿**在每次请求中创建和销毁它。应该在应用程序启动时创建一个单例。
*   **连接字符串**：使用 `ConfigurationOptions` 对象而不是纯字符串，以便更灵活地配置（如超时、重试策略）。

```csharp
var options = new ConfigurationOptions
{
    EndPoints = { "localhost:6379" },
    AbortOnConnectFail = false, // 连接失败时不抛出异常，而是重试
    ConnectTimeout = 5000,
    SyncTimeout = 5000
};
var redis = ConnectionMultiplexer.Connect(options);
```

## 2. Key 命名规范

*   **使用冒号分隔**：`object_type:id:field`。例如 `user:1001:email`。
*   **保持简洁**：Key 也是占用内存的，不要太长，但要保持可读性。
*   **统一前缀**：为不同应用或模块添加前缀，防止 Key 冲突。

## 3. 序列化

Redis 只存储字节 (byte[])。在 .NET 中通常存储 JSON 字符串。

*   **System.Text.Json**：性能好，.NET 内置。
*   **Newtonsoft.Json**：功能丰富，老牌库。
*   **MessagePack / Protobuf**：二进制序列化，体积更小，速度更快，适合对性能和内存要求极高的场景。

## 4. 避免危险命令

*   **KEYS \***：**绝对禁止**在生产环境使用。它会遍历所有 Key，导致 Redis 阻塞。请使用 `SCAN` 命令代替。
*   **FLUSHALL / FLUSHDB**：小心使用。

## 5. Redis 知识点：常见性能问题

### BigKey (大 Key)
*   **定义**：Value 占用内存过大（如 String > 10KB）或元素数量过多（如 List/Hash/Set 元素 > 5000）。
*   **危害**：
    *   **网络阻塞**：传输耗时久。
    *   **线程阻塞**：Redis 单线程处理，操作 BigKey 会导致其他请求排队。
    *   **删除阻塞**：`DEL` 一个 BigKey 耗时很久（建议使用 `UNLINK` 异步删除）。
*   **发现**：使用 `redis-cli --bigkeys` 扫描。

### HotKey (热 Key)
*   **定义**：某个 Key 的访问频率极高（如秒杀商品的库存）。
*   **危害**：流量集中在某一个分片节点，导致该节点网卡/CPU 打满，而其他节点空闲。
*   **解决**：
    *   **本地缓存**：在应用层（如 .NET MemoryCache）再加一层缓存。
    *   **拆分 Key**：将 `product:100` 拆分为 `product:100:1`, `product:100:2` 分散到不同节点。

## 6. 内存管理

*   **设置过期时间**：绝大多数缓存都应该有过期时间，防止内存无限增长。

*   **内存淘汰策略**：在 `redis.conf` 中配置 `maxmemory-policy`（如 `allkeys-lru`），当内存满时自动删除旧数据。

## 6. 异步编程

Redis 操作是 I/O 密集型的。**始终使用 Async 方法**（如 `StringGetAsync`），避免阻塞线程池线程，提高吞吐量。
