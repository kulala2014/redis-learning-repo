# 1. Redis 与 .NET 简介及环境搭建

## 什么是 Redis？

Redis (Remote Dictionary Server) 是一个开源的、基于内存的数据结构存储系统。它可以用作数据库、缓存和消息中间件。

## 为什么在 .NET 中使用 Redis？

- **高性能缓存**：作为内存数据库，读写速度极快，适合缓存热点数据。
- **丰富的数据结构**：不仅仅是 Key-Value，还支持 List, Set, Hash 等，适合解决复杂业务场景（如排行榜、计数器）。
- **分布式 Session**：在负载均衡环境中存储用户会话。
- **分布式锁**：解决并发控制问题。

## Redis 核心知识点

### 1. 单线程模型
Redis 的核心业务处理（命令执行）是**单线程**的。
*   **优势**：避免了多线程上下文切换的开销，没有锁竞争问题。
*   **劣势**：无法利用多核 CPU（但在 Redis 6.0 后引入了多线程 I/O 处理网络请求，核心命令依然是单线程）。
*   **注意**：因为是单线程，如果执行一个耗时命令（如 `KEYS *` 或操作 BigKey），会阻塞后续所有请求。

### 2. 持久化机制
Redis 虽然是内存数据库，但也支持将数据写入磁盘。
*   **RDB (Redis Database)**: 快照模式。在指定时间间隔内生成数据集的时间点快照。
    *   *优点*：恢复速度快，文件紧凑。
    *   *缺点*：可能会丢失最后一次快照后的数据。
*   **AOF (Append Only File)**: 追加模式。记录服务器接收到的每一个写操作。
    *   *优点*：数据更安全，最多丢失 1 秒数据（取决于 `fsync` 策略）。
    *   *缺点*：文件体积通常比 RDB 大，恢复速度慢。
*   **混合持久化**：Redis 4.0+ 支持 RDB + AOF 混合使用，结合两者优点。

## 环境搭建

### 1. 安装与运行 Redis (本地模式)

由于你选择不使用 Docker，请根据你的操作系统选择安装方式。

#### Windows 用户
Redis 官方不直接支持 Windows，但有以下两种主流方案：

1.  **使用 WSL2 (推荐)**:
    *   如果你安装了 WSL2 (Windows Subsystem for Linux)，可以在 Ubuntu 等发行版中直接安装：
        ```bash
        sudo apt-get update
        sudo apt-get install redis-server
        sudo service redis-server start
        ```
2.  **使用 Windows 移植版**:
    *   下载地址：[tporadowski/redis](https://github.com/tporadowski/redis/releases)
    *   下载 `.zip` 解压后，双击 `redis-server.exe` 即可启动。

#### macOS 用户
使用 Homebrew 安装：
```bash
brew install redis
brew services start redis
```

#### 验证安装
启动后，打开一个新的终端窗口，输入 `redis-cli`，然后输入 `ping`。如果返回 `PONG`，说明 Redis 已成功在本地启动。

### 2. .NET 客户端库

在 .NET 生态中，最主流的 Redis 客户端是 **StackExchange.Redis**。

安装方式：

```bash
dotnet add package StackExchange.Redis
```

对于 ASP.NET Core 的分布式缓存抽象，还需要：

```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

## 连接 Redis

在使用 `StackExchange.Redis` 时，核心对象是 `ConnectionMultiplexer`。

**重要原则**：`ConnectionMultiplexer` 旨在被**复用**。在整个应用程序生命周期中，应该只创建一个实例（单例模式）。

```csharp
using StackExchange.Redis;

// 创建连接
var redis = ConnectionMultiplexer.Connect("localhost");

// 获取数据库 (Redis 默认有 16 个数据库，索引 0-15)
var db = redis.GetDatabase();
```
