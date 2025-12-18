
# 6. Kafka 基础入门 (vs Redis)

既然你已经熟悉了 Redis，学习 Kafka 最好的方式就是对比学习。

## 1. 核心概念对比

| 概念 | Redis (Pub/Sub & Streams) | Kafka | 区别 |
| :--- | :--- | :--- | :--- |
| **消息模型** | Pub/Sub (广播), Streams (日志) | Log (日志) | Kafka 专为大规模日志流设计，持久化能力更强。 |
| **持久化** | Pub/Sub 不持久化; Streams 可持久化 | **默认持久化** (保存到磁盘) | Kafka 消息默认保存 7 天（可配置），Redis 取决于内存/RDB/AOF。 |
| **消费模式** | 推送 (Push) / 拉取 (Pull) | **拉取 (Pull)** | Kafka 消费者自己控制读取速度，适合高吞吐。 |
| **逻辑结构** | Channel / Stream Key | **Topic** (主题) | 概念类似，都是消息的分类。 |
| **并行处理** | Consumer Group (Streams) | **Consumer Group** | 概念几乎一致，用于负载均衡。 |
| **物理结构** | 单线程 (大部分) | **Partition** (分区) | Kafka 通过分区实现并行读写，吞吐量极高。 |

## 2. 核心应用场景：削峰填谷 (Peak Shaving)

这是 Kafka 最经典的应用场景之一，常用于秒杀、大促等高并发场景。

### 1. 什么是"削峰"？
想象一个**水库**（Kafka）。
*   **上游（用户请求）**：突然下了一场暴雨（流量激增，10万 QPS）。
    *   *注：QPS (Queries Per Second) 指每秒查询率，即每秒钟处理的请求数量。*
*   **下游（数据库/后端服务）**：排水管道很细，每秒只能排 1000 吨水（1000 QPS）。
*   **如果没有水库**：洪水直接冲向管道，管道瞬间爆裂（数据库宕机，系统崩溃）。
*   **如果有水库**：
    1.  洪水先全部流进水库（Kafka 快速写入）。
    2.  水库把水蓄起来（消息堆积）。
    3.  下游管道按照自己的节奏，慢慢地从水库里放水（Consumer 匀速消费）。

### 2. 为什么 Kafka 能削峰？
*   **极高的写入性能**：Kafka 采用顺序写磁盘（Sequential I/O）和零拷贝（Zero Copy）技术，单机写入吞吐量轻松达到几十万 QPS。它能瞬间接住巨大的流量。
*   **巨大的堆积能力**：Kafka 消息是持久化到磁盘的，不像 Redis 那样受限于内存。它可以轻松堆积几亿条消息而不影响性能。
*   **拉取模式 (Pull)**：消费者（下游）是**主动拉取**消息的。下游处理多快，就拉多快。不会因为上游发太快而被压垮。

### 3. 实战流程
1.  **用户请求** -> **网关** -> **Kafka Producer** (极速写入，不处理业务)。
2.  **Kafka** (蓄水，堆积消息)。
3.  **后台服务 (Consumer)** -> **拉取消息** -> **慢速处理业务** (写库、调第三方接口)。

---

## 3. 学习路径

### Level 1: 生产者 (Producer)
就像 Redis 的 `PUBLISH` 或 `XADD`。
我们在 `KafkaController` 中实现了发送消息：

```csharp
var config = new ProducerConfig { BootstrapServers = "localhost:9092" };
using var producer = new ProducerBuilder<Null, string>(config).Build();
await producer.ProduceAsync("test-topic", new Message<Null, string> { Value = "Hello Kafka" });
```

### Level 2: 消费者 (Consumer)
就像 Redis 的 `SUBSCRIBE` 或 `XREADGROUP`。
我们在 `KafkaConsumerService` 中实现了后台监听：

```csharp
var config = new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "test-consumer-group", // 消费者组 ID
    AutoOffsetReset = AutoOffsetReset.Earliest // 如果没有记录，从头开始读
};
using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
consumer.Subscribe("test-topic");

while (!token.IsCancellationRequested)
{
    var result = consumer.Consume(token);
    Console.WriteLine(result.Message.Value);
}
```

### Level 3: 消费者组 (Consumer Group)
这是 Kafka 最强大的特性之一，Redis Streams 借鉴了这一点。
*   **单播**：如果多个消费者属于**同一个 Group**，消息会在它们之间**负载均衡**（每条消息只被处理一次）。
*   **广播**：如果多个消费者属于**不同的 Group**，每个 Group 都会收到完整的消息副本。

#### 通俗理解：快递分拣比喻
如果觉得抽象，可以用**“快递分拣”**来理解：

1.  **场景设定**
    *   **Topic (传送带)**：源源不断送来包裹。
    *   **Partition (跑道)**：传送带拆成了 4 条跑道 (0, 1, 2, 3)。
    *   **Consumer Group (红马甲搬运队)**：一个部门。
    *   **Consumer (老张)**：具体的员工。

2.  **核心逻辑**
    *   **只有老张 (1人)**：老张一个人在 4 条跑道间来回跑，累且慢。
    *   **招了帮手 (4人)**：老张、老李、老王、老赵各守一条跑道。**效率最高 (负载均衡)**。
    *   **人太多 (5人)**：实习生小刘只能在旁边看着，因为**一条跑道只能由一个人负责** (避免抢单)。
    *   **来了蓝队 (新 Group)**：蓝马甲审计队来记账。Kafka 会把包裹“影分身”，红队搬走一份，蓝队记账一份。**互不干扰 (广播)**。

**总结**：Consumer Group 让一群人像一个人一样工作（分工），同时允许多个团队并行处理同一份数据（广播）。

## 3. 消息顺序保证 (Ordering)

这是一个非常重要的面试题和实战概念。

### 核心规则
**Kafka 只能保证同一个 Partition 内的消息是有序的，无法保证跨 Partition 的全局顺序。**

### 场景 1：必须保证全局顺序
如果你的业务要求所有消息必须严格按照发送顺序处理（例如：全局操作日志）。
*   **解决方案**：创建 Topic 时只设置 **1 个 Partition**。
*   **代价**：牺牲了并发性能，只能有一个消费者处理消息。

### 场景 2：只需保证局部顺序 (推荐)
大多数业务只需要保证"相关"数据的顺序。例如：同一个订单的状态变更（创建 -> 支付 -> 发货）必须有序，但订单 A 和订单 B 之间不需要排序。
*   **解决方案**：使用 **Message Key (分区键)**。
*   **原理**：Kafka 生产者客户端使用哈希算法（默认是 Murmur2）计算 Key 的哈希值，然后对 Partition 总数取模。
    *   公式：`Partition = Hash(Key) % TotalPartitions`
    *   **结果**：只要 Partition 数量不变，同一个 Key 永远算出同一个 Partition ID。
*   **代码示例**:

```csharp
// 生产者发送时指定 Key (例如 OrderId)
var message = new Message<string, string> 
{ 
    Key = "Order-1001", 
    Value = "PaymentReceived" 
};
await producer.ProduceAsync("orders-topic", message);
```

这样，所有 `Key="Order-1001"` 的消息都会进入同一个分区（比如 Partition 0），从而保证消费者能按顺序读到它们。

## 4. 如何决定 Partition 的数量？

Partition 的数量直接决定了 Kafka 的**并发吞吐量**。

### 核心公式
$$ \text{Partition Count} \ge \max(\text{Target Producer Throughput}, \text{Target Consumer Throughput}) $$

### 关键考量指标：

1.  **消费者并发度 (Consumer Parallelism)**
    *   **规则**：一个 Partition 同一时刻只能被同一个 Consumer Group 中的**一个**消费者消费。
    *   **场景 A (Consumers < Partitions)**: 这是一个常见场景。例如有 3 个 Partition，但只有 1 个消费者。结果是：**这 1 个消费者会负责消费所有 3 个 Partition 的数据**。数据**不会**丢失，只是处理压力全在一个人身上。
    *   **场景 B (Consumers > Partitions)**: 例如有 1 个 Partition，但启动了 3 个消费者。结果是：**只有 1 个消费者在工作，另外 2 个会闲置 (Idle)**。
    *   **建议**：如果你预计未来需要 5 个消费者实例来处理流量，那么 Partition 数量至少设为 5。

2.  **吞吐量需求 (Throughput)**
    *   Partition 是 Kafka 读写的基本单位。更多的 Partition 意味着可以利用更多的 Broker 磁盘 I/O 和网络带宽。
    *   **建议**：如果单 Partition 吞吐量是 $P$，总目标吞吐量是 $T$，那么 Partition 数量 $N = T / P$。

3.  **系统资源限制**
    *   每个 Partition 都会占用 Broker 的文件句柄和内存。
    *   **警告**：不要盲目设置过大（如几千个），这会增加 Leader 选举的时间和元数据管理的开销。

### 经验法则 (Rule of Thumb)
*   **小型业务**：1 ~ 3 个 Partition。
*   **中型业务**：3 ~ 10 个 Partition（通常能满足大部分需求）。
*   **大型业务**：根据压测结果决定，通常是 Broker 数量的倍数。

## 5. 什么是 Offset (偏移量)？

**Offset 是 Kafka 中最核心的概念之一，它是一个简单的整数（Long 类型）。**

你可以把它想象成**数组的下标**或者**书的页码**。

### 1. 两个层面的 Offset
*   **Message Offset (消息坐标)**:
    *   每条消息进入 Partition 时，都会被分配一个**唯一的、单调递增**的 ID，这就是 Offset。
    *   例如：Partition 0 的第 1 条消息 Offset 是 0，第 2 条是 1，以此类推。
    *   **作用**：唯一标识 Partition 中的一条消息。

*   **Consumer Offset (消费进度)**:
    *   消费者组 (Consumer Group) 需要记录自己"读到哪里了"。
    *   **作用**：作为检查点 (Checkpoint)。如果消费者崩溃重启，它会读取这个 Offset，从上次断开的地方继续消费，从而保证**不丢失、不重复**（理想情况下）。

### 2. 提交 (Commit) 的概念
当消费者处理完一条消息后，它需要告诉 Kafka："我已经处理到 Offset X 了，请记下来"。这个动作叫 **Commit**。
*   **Current Offset**: 消费者当前正在处理的消息。
*   **Committed Offset**: 消费者已经确认处理完毕，并汇报给 Kafka 的进度。

### 3. 图解
```text
[ Message 0 ] [ Message 1 ] [ Message 2 ] [ Message 3 ] ...
      ^             ^
      |             |
 已处理并提交      当前正在处理
(Committed: 1)   (Current: 2)
```

## 6. 消费可靠性指南 (Reliability)

### 1. AutoOffsetReset 策略选择
当你的消费者组 (Consumer Group) **第一次启动**，或者**Offset 丢失**（比如数据过期）时，该怎么办？

*   **`Earliest` (推荐用于业务系统)**
    *   **行为**：从 Partition 的**最开始**（第一条消息）开始读。
    *   **优点**：绝对**不丢失**任何历史消息。即使消费者宕机 3 天，重启后也能把这 3 天的订单全补回来。
    *   **缺点**：**会重复消费**。如果你的 Offset 丢失了，它会把几年前的订单也重发一遍。
    *   **关键配套**：**必须配合幂等性设计**（见下文）。因为"重复执行"可以通过代码防御，但"丢失数据"是无法挽回的。
    *   **适用**：订单、支付、库存等核心业务。

*   **`Latest` (推荐用于监控/日志)**
    *   **行为**：只读**启动之后**新到达的消息。
    *   **优点**：启动快，不处理历史包袱。
    *   **缺点**：**会丢失数据**。如果消费者宕机了一段时间，这段时间内的消息就**永久丢失**了。
    *   **适用**：实时监控大屏、日志收集、非关键通知。

### 2. 如何处理"重复消费"？(幂等性)
你提到的场景："处理了消息 -> 还没来得及 Commit -> 崩溃 -> 重启 -> 收到重复消息"。
这是 Kafka 默认的 **At-Least-Once (至少一次)** 投递保证。

**解决方案：幂等性 (Idempotency)**
不要试图让 Kafka 做到"精确一次"（Exactly-Once 很难且性能差），而是让你的**业务逻辑**不怕重复执行。

**通用模式 (Redis 去重)**：
1.  每条消息必须有一个唯一 ID (MessageId / OrderId)。
2.  消费时，先查 Redis：`EXISTS processed:{MessageId}`？
3.  如果存在 -> 直接跳过 (Commit 并 return)。
4.  如果不存在 -> 执行业务逻辑 -> 写入 Redis -> Commit。

```csharp
// 伪代码示例
public async Task HandleMessage(string msgId, string data)
{
    // 1. 检查是否处理过 (幂等性检查)
    if (await _redis.KeyExistsAsync($"processed:{msgId}"))
    {
        _logger.LogInformation("Duplicate message detected, skipping.");
        return; 
    }

    // 2. 执行业务 (例如扣减库存)
    await _inventoryService.DeductStock(data);

    // 3. 标记为已处理 (设置过期时间，比如 24小时)
    await _redis.StringSetAsync($"processed:{msgId}", "1", TimeSpan.FromHours(24));
}
```

## 7. 动手实验

1.  **启动 Kafka**: 确保 Docker 容器已运行 (`docker compose up -d`)。
2.  **运行程序**: 启动我们的 .NET API。
3.  **发送消息**: 使用 Swagger 调用 `POST /api/Kafka/produce?message=HelloKafka`。
4.  **观察日志**: 查看控制台输出，你应该能看到 `[Kafka Received]` 的日志。

## 8. 关键配置说明

*   **BootstrapServers**: Kafka 集群的地址列表（这里是 `localhost:9092`）。
*   **GroupId**: 标识消费者所属的组。
*   **AutoOffsetReset**: 当消费者第一次加入，或者 Offset 丢失时，从哪里开始读？
    *   `Earliest`: 从最早的消息开始（相当于重播）。
    *   `Latest`: 只读最新的消息（相当于直播）。
