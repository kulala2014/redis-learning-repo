# Redis in .NET 学习文档

欢迎来到 Redis in .NET 系统性学习指南。本文档旨在帮助开发者从零开始掌握在 .NET 环境下使用 Redis 的技能。

## 目录

1.  [**简介与环境搭建**](./01-introduction.md)
    *   什么是 Redis？
    *   Docker 环境搭建
    *   StackExchange.Redis 客户端安装

2.  [**基础数据结构实战**](./02-basic-data-structures.md)
    *   String (字符串)
    *   Hash (哈希)
    *   List (列表)
    *   Set (集合)
    *   Sorted Set (有序集合)

3.  [**ASP.NET Core 集成与缓存策略**](./03-aspnet-core-integration.md)
    *   依赖注入配置
    *   IDistributedCache 使用
    *   Cache-Aside (旁路缓存) 模式
    *   缓存穿透/击穿/雪崩解决方案

4.  [**进阶模式**](./04-advanced-patterns.md)
    *   Pub/Sub (发布订阅)
    *   Transactions (事务)
    *   Distributed Lock (分布式锁)
    *   Pipelining (管道)

5.  [**最佳实践与性能优化**](./05-best-practices.md)
    *   连接管理 (单例)
    *   Key 命名规范
    *   序列化选择
    *   危险命令与内存管理

## 配套代码

本文档配套的代码示例位于项目根目录：

*   `RedisBasics/`: 控制台应用，对应第 2 章内容。
*   `RedisApi/`: Web API 应用，对应第 3 章内容。
