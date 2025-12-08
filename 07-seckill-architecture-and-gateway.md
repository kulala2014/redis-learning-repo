# 秒杀架构与网关设计学习笔记

本文档整理了关于高并发秒杀系统架构、Redis 库存扣减、数据库交互以及网关/反向代理的核心概念。

## 1. 现实世界中的秒杀流程

一场秒杀活动不仅仅是代码逻辑，更是一个涉及全链路的系统工程。核心思想是**流量漏斗**：层层拦截，只让极少数有效请求到达脆弱的数据库。

### 1.1 生命周期
1.  **运营配置阶段**
    *   选品、设置秒杀独立库存。
    *   活动状态管理（待发布、审核中）。
2.  **系统预热阶段 (关键)**
    *   **CDN 推送**：将页面 HTML/CSS/JS/图片推送到离用户最近的 CDN 节点。
    *   **缓存预热 (Cache Warming)**：将数据库中的库存同步到 Redis (`InitData`)。
    *   **时间同步**：前端倒计时依赖服务器时间。
3.  **活动进行中 (流量削峰)**
    *   **网关拦截**：Nginx/Gateway 挡住 90% 的恶意脚本和超限流量。
    *   **Redis 预扣减**：应用层通过 Lua 脚本扣减缓存库存，挡住 99% 的无效抢购。
    *   **MQ 异步下单**：抢到资格的用户请求进入消息队列 (Kafka/RabbitMQ)。
    *   **数据库写入**：后台消费者慢慢消费消息，创建订单并扣减 DB 库存。
4.  **支付与回滚**
    *   处理支付超时，回滚 Redis 和数据库库存。

### 1.2 架构图示
```mermaid
graph TD
    User[用户] --> CDN[CDN (静态资源)]
    User --> Gateway[网关 (Nginx/YARP)]
    Gateway --限流/黑名单--> Block[直接返回排队中]
    Gateway --有效请求--> App[应用服务 (.NET API)]
    App --> Redis[(Redis 缓存)]
    Redis --库存不足--> Fail[返回秒杀结束]
    Redis --扣减成功--> MQ[消息队列 (Kafka)]
    MQ --> Consumer[后台消费者]
    Consumer --> DB[(数据库)]
```

## 2. 代码实现演进 (Redis + Database)

在 `SeckillController.cs` 中，我们模拟了核心的扣减逻辑。

*   **初始化 (`InitData`)**:
    *   从数据库读取商品库存。
    *   写入 Redis `product:stock`。
*   **核心扣减 (`BuyWithLua`)**:
    *   **第一步 (Redis)**: 使用 Lua 脚本原子性判断并扣减 Redis 库存。这是抗并发的关键。
    *   **第二步 (DB)**: (演示中) 同步执行 SQL `UPDATE Products SET Stock = Stock - 1`。
    *   *注意*: 生产环境中，第二步应替换为发送 MQ 消息，实现“写库解耦”。

## 3. 网关与反向代理

### 3.1 核心概念辨析

| 概念 | 英文 | 角色 | 例子 | 谁被藏起来了？ |
| :--- | :--- | :--- | :--- | :--- |
| **正向代理** | Forward Proxy | **客户端**的代理 | VPN, 代购 | **客户端** (服务器不知道是谁买的) |
| **反向代理** | Reverse Proxy | **服务器**的代理 | Nginx, 10086客服 | **服务器** (客户端不知道是谁服务的) |

#### 图解：正向代理 vs 反向代理
```mermaid
graph LR
    subgraph Forward_Proxy_Scenario [正向代理 (代购)]
        ClientA[用户 A] --> ProxyF[正向代理 (VPN/代购)]
        ClientB[用户 B] --> ProxyF
        ProxyF --> Server[目标服务器 (Google/商店)]
        style ProxyF fill:#f9f,stroke:#333,stroke-width:2px
        style ClientA fill:#fff,stroke:#333
        style ClientB fill:#fff,stroke:#333
        style Server fill:#eee,stroke:#333
    end

    subgraph Reverse_Proxy_Scenario [反向代理 (网关)]
        User1[用户 1] --> ProxyR[反向代理 (Nginx/网关)]
        User2[用户 2] --> ProxyR
        ProxyR --> Server1[服务器 1]
        ProxyR --> Server2[服务器 2]
        style ProxyR fill:#bbf,stroke:#333,stroke-width:2px
        style User1 fill:#fff,stroke:#333
        style User2 fill:#fff,stroke:#333
        style Server1 fill:#eee,stroke:#333
        style Server2 fill:#eee,stroke:#333
    end
```

*   **反向代理的作用**：
    *   **负载均衡**：把请求分发给空闲的服务器。
    *   **安全防护**：隐藏真实服务器 IP，抵挡攻击。
    *   **缓存加速**：直接返回静态资源。
    *   **SSL 卸载**：统一处理 HTTPS 加密解密。
## 4. 限流策略 (Rate Limiting)

如何防止 100万 请求击垮只有 100 库存的系统？

#### 流量漏斗模型
```mermaid
graph TD
    Total[总流量: 100万 QPS] -->|1. Nginx/WAF 拦截恶意IP| Layer1[剩余: 50万 QPS]
    Layer1 -->|2. 网关全局限流 (Redis令牌桶)| Layer2[剩余: 2000 QPS]
    Layer2 -->|3. 应用层 Redis 预扣减| Layer3[剩余: 100 QPS (抢到资格)]
    Layer3 -->|4. 消息队列削峰| Layer4[数据库写入: 100 TPS]
    
    style Total fill:#f00,stroke:#333,color:#fff
    style Layer1 fill:#f66,stroke:#333,color:#fff
    style Layer2 fill:#f99,stroke:#333
    style Layer3 fill:#9f9,stroke:#333
    style Layer4 fill:#0f0,stroke:#333
```

1.  **Nginx 层 (漏桶/令牌桶)**:
网关本质上是反向代理，但它更懂业务逻辑：
*   **身份认证 (Auth)**：校验 Token、权限。
*   **限流熔断 (Rate Limiting)**：控制流量，保护后端。
*   **协议转换**：HTTP -> gRPC。
*   **请求聚合**：一次请求调用多个微服务并合并结果。

### 3.3 常见工具对比

1.  **Nginx**:
    *   **定位**: 高性能反向代理服务器。
    *   **优势**: 性能极致，资源占用少。
    *   **场景**: 最外层入口，静态资源服务，简单的负载均衡。
2.  **Ocelot**:
    *   **定位**: 老牌 .NET API 网关。
    *   **优势**: 功能全 (聚合、鉴权)，配置简单 (JSON)。
    *   **劣势**: 性能一般，维护活跃度下降。
3.  **YARP (Yet Another Reverse Proxy)**:
    *   **定位**: 微软官方高性能反向代理工具箱。
    *   **优势**: **性能极高**，高度可定制 (C# 代码控制)，原生集成 ASP.NET Core 管道。
    *   **场景**: .NET Core 项目构建高性能网关的首选。

## 4. 限流策略 (Rate Limiting)

如何防止 100万 请求击垮只有 100 库存的系统？

1.  **Nginx 层 (漏桶/令牌桶)**:
    *   基于 IP 限流，超限直接返回 503。
    *   效率最高，在流量进入应用前就挡回去。
2.  **分布式限流 (Redis + Lua)**:
    *   多台网关共享一个 Redis 计数器/令牌桶。
    *   实现全局流量控制 (如：全站总 QPS 不超过 2000)。
3.  **应用层限流 (.NET Middleware)**:
    *   使用 `.NET 7+` 的 `RateLimiting` 中间件。
    *   作为最后一道防线，保护具体 API。

---
*最后更新: 2025-11-29*
