# redis-learning-repo
# Redis Mastery Roadmap for .NET Developers

> 系统式、可执行的 Redis 学习计划，适合已有 .NET 开发经验、想从入门走向企业级实战的你。

---

## 🎯 学习目标

- 掌握 Redis 核心概念、数据结构与命令
- 理解持久化、复制、哨兵、集群等高可用机制
- 能在 .NET 项目中设计高效的缓存、限流、分布式锁等组件
- 建立完整的实验与监控体系，形成工程化输出

---

## 🗺️ 学习里程碑概览（4~6 周）

| 阶段 | 时间 | 核心主题 | 产出 |
|------|------|----------|------|
| **Phase 1** 基础夯实 | Week 1 | 数据结构、命令、持久化、事务/Lua | Console Demo、命令清单、持久化对比表 |
| **Phase 2** 高可用与运维 | Week 2-3 | 内存管理、复制、哨兵、集群、性能诊断 | Docker Compose 集群、性能报告、架构图 |
| **Phase 3** 企业级实践 | Week 4-5 | 缓存策略、分布式锁、限流、队列、监控 | ASP.NET Core 模块、Lua 工具库、监控仪表盘 |
| **Phase 4** （可选） | Week 6+ | 源码阅读、底层扩展 | 对象模型笔记、阅读报告 |

---

## 📆 每周详细计划

### Phase 1 · Redis 基础（Week 1）

| Day | 主题 | 任务 | 验收 |
|-----|------|------|------|
| 1 | 架构总览 | 阅读 Redis Intro；画出架构草图；熟悉 `SET`/`DEL`/`TTL` | 小结 200 字 |
| 2 | String & Hash | 用 `StackExchange.Redis` 缓存用户信息；练习 `GET`/`MSET`/`HSET` | Console Demo |
| 3 | List/Set/ZSet | 设计“最新消息”“去重集合”“排行榜”案例 | 命令流程记录 |
| 4 | 事务 & Lua | 使用 `MULTI/EXEC`、Pipeline；编写库存扣减 Lua 脚本 | C# 调用 Lua |
| 5 | 持久化 | 对比 RDB/AOF/混合；动手触发并观察文件 | 对比表 + 截图 |
| 6 | 复习自测 | 命令复杂度笔记；10 个练习题 | Markdown 笔记 |
| 7 | 休整/补缺 | 整理疑问，准备进阶 | Q&A 清单 |

**参考资源**

- 《Redis 设计与实现》Part I
- Redis 官方文档：Data Types、Persistence
- B 站：小林 coding Redis 基础

---

### Phase 2 · 高可用与运维（Week 2-3）

#### Week 2
- **Day1**：内存淘汰策略实验（设置 `maxmemory`，对比 LRU/LFU/Random）
- **Day2**：慢查询与 Pipeline 对比；产出性能报告
- **Day3**：搭建主从（Docker Compose），在 .NET 中实现读写分离
- **Day4**：哨兵 Sentinel 自动故障转移测试
- **Day5**：Redis Cluster 创建、reshard 操作，输出问答笔记

#### Week 3
- **Day1**：Watch + Lua 并发控制实验
- **Day2**：热点 Key 诊断 (`redis-cli --bigkeys/--hotkeys`)
- **Day3**：安全配置（AUTH、TLS、ACL）
- **Day4**：绘制完整高可用架构图
- **Day5**：模拟面试，回答 10 个高可用问题

**参考资源**

- 《Redis 深度历险》
- Redis 官方文档：Replication、Sentinel、Cluster
- 极客时间《Redis 核心技术与实战》

---

### Phase 3 · 企业级应用实战（Week 4-5）

#### Week 4 · 缓存体系
- Cache-Aside 模式 Demo（ASP.NET Core，读缓存失败回退 DB）
- 防缓存击穿/穿透/雪崩策略（随机过期、布隆过滤器、互斥锁）
- 编写可复用的缓存中间件（含测试）
- 输出缓存策略说明文档

#### Week 5 · 分布式工具集
- 分布式锁封装：SET NX + PX + Lua 解锁，压测重试机制
- Redis Stream 任务队列：生产者/消费者 + 死信处理
- Lua 脚本实现滑动窗口限流中间件
- RedisInsight + Prometheus + Grafana 搭建监控仪表盘

**参考资源**

- 阿里云 Redis 白皮书（缓存策略章节）
- Netflix/GitHub 技术博客：Stream & 限流实践
- 开源项目：Hangfire.Redis、CAP、StackExchange.Redis.Extensions

---

### Phase 4 · 深入理解（可选）

- 阅读 Redis 源码（对象系统、事件循环、内存分配）
- 探索 Redis 模块（Bloom Filter、Graph、JSON）
- 尝试编写一个简单 Redis Module（C/Go）

---

## 🧰 .NET 工程师工具箱

| 类别 | 推荐 | 说明 |
|------|------|------|
| 客户端 | [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/) | 官方维护，性能优异 |
| 配置管理 | `.NET` Options + `IConfiguration` | 分环境配置连接信息 |
| 集成测试 | [Testcontainers for .NET](https://dotnet.testcontainers.org/) | 启动临时 Redis |
| Profiling | MiniProfiler.Redis、dotnet-monitor | 分析命令耗时 |
| 可视化 | RedisInsight、Another Redis Desktop Manager | 图形化观察数据 |
| 监控 | Prometheus Redis Exporter、Grafana Dashboard | 追踪 QPS、延迟、内存 |

---

## 📚 参考书籍 & 课程

- 《Redis 设计与实现》——结构与原理必读
- 《Redis 深度历险》——实践案例 + 底层剖析
- 《Redis 实战》(Manning) ——场景驱动
- 极客时间《Redis 核心技术与实战》——系统课程
- MIT 6.824 分布式系统（部分课件讲到复制机制）

---

## ✅ 进度追踪模板

```markdown
### Week 1 Checklist
- [ ] 了解 Redis 架构与命令分类
- [ ] 完成 StackExchange.Redis 基础 Demo
- [ ] Lua 脚本扣减库存成功运行
- [ ] 持久化方式对比表已整理

### Week 2 Checklist
- [ ] 内存淘汰策略实验完成
- [ ] 主从 + Sentinel 环境搭建并演练 failover
- [ ] Pipeline 性能报告
- [ ] Cluster 场景问答

...
