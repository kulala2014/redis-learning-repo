# Redis in .NET 学习指南

这是一个用于系统性学习在 .NET 中使用 Redis 的示例项目。

📚 **[点击这里查看完整的系统性学习文档](./docs/README.md)**

## 项目结构

- **RedisBasics**: 控制台应用程序，演示 Redis 的基础数据结构操作。
- **RedisApi**: ASP.NET Core Web API，演示 Redis 作为分布式缓存 (`IDistributedCache`) 的使用以及依赖注入。

## 环境准备

你需要一个运行中的 Redis 实例。本项目包含一个 `docker-compose.yml` 文件，如果你安装了 Docker，可以直接运行：

```bash
docker-compose up -d
```

这将启动一个 Redis 容器，监听 `localhost:6379`。

## 学习路径

### 1. 基础操作 (RedisBasics)

这个项目演示了如何使用 `StackExchange.Redis` 库进行最基本的操作。

**涵盖内容：**
- 连接 Redis
- **String**: 简单的键值对，过期时间，自增。
- **List**: 队列/栈操作 (Push/Pop)。
- **Hash**: 存储对象/字典。
- **Set**: 无序不重复集合。
- **Sorted Set**: 带分数的有序集合 (排行榜)。

**运行方式：**

```bash
cd RedisBasics
dotnet run
```

请阅读 `RedisBasics/Program.cs` 源码，配合输出结果理解每一步操作。

### 2. Web API 与 缓存 (RedisApi)

这个项目演示了在实际 Web 应用中如何集成 Redis。

**涵盖内容：**
- **依赖注入**: 如何注册 `IConnectionMultiplexer` (单例)。
- **IDistributedCache**: 使用 .NET 标准的缓存抽象接口。
- **缓存策略**: 缓存命中、缓存失效、过期时间设置。

**运行方式：**

```bash
cd RedisApi
dotnet run
```

**测试接口：**

1.  **获取天气 (带缓存)**:
    - 访问: `http://localhost:5261/weatherforecast` (端口可能不同，请查看启动日志)
    - 第一次访问会比较慢 (模拟耗时操作)，并打印 "Fetching from source"。
    - 10秒内再次访问会很快，并打印 "Returning from cache"。

2.  **直接操作 Redis**:
    - 设置值: `POST /api/values?key=test&value=123`
    - 获取值: `GET /api/values/test`

## 关键库

- **StackExchange.Redis**: .NET 社区最流行的 Redis 客户端，高性能。
- **Microsoft.Extensions.Caching.StackExchangeRedis**: 微软官方提供的 `IDistributedCache` 实现，基于 StackExchange.Redis。

## 进阶建议

在掌握了上述内容后，你可以尝试：
1.  **发布/订阅 (Pub/Sub)**: 实现简单的消息通知。
2.  **事务 (Transactions)**: 学习 `ITransaction`。
3.  **Redis Streams**: 处理流式数据。
4.  **RedLock**: 分布式锁的实现。
