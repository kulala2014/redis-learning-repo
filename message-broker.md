# Message Broker Architecture - A Domain-Driven Design Perspective

本文档从 **领域驱动设计 (DDD)** 的视角，对 `message-broker` 解决方案的架构进行深度梳理。

## 1. 战略设计 (Strategic Design)

### 1.1 界限上下文 (Bounded Context)
**Message Broker** 被定义为一个独立的界限上下文。它的核心职责是作为企业内部各系统之间的 **集成胶水 (Integration Glue)**。

*   **核心域 (Core Domain)**: 消息的可靠投递、路由分发、以及对多云环境（AWS/Azure）的抽象适配。
*   **上游上下文**: 业务系统（如 POS、订单系统），它们是消息的 **生产者 (Producers)**。
*   **下游上下文**: 第三方集成、ERP、数据分析系统，它们是消息的 **消费者 (Consumers)**。

### 1.2 通用语言 (Ubiquitous Language)
在整个解决方案中，以下术语具有严格定义的业务含义：
*   **Message (消息)**: 业务发生的原子事件数据单元。
*   **Topic (主题)**: 消息的逻辑分类管道。
*   **Subscription (订阅)**: 消费者对特定主题的订阅关系。
*   **Processor (处理器)**: 对特定类型消息进行业务处理的逻辑单元。

---

## 2. 战术设计与分层架构 (Tactical Design & Layered Architecture)

本项目遵循严格的分层架构，依赖倒置原则 (DIP) 是其核心。

### 2.1 领域层 (Domain Layer)
**项目**: `NEXTEP.MessageBroker.Core`
*地位：系统的核心，不依赖任何外部技术细节。*

*   **实体 (Entities)**:
    *   **`Message`**: (聚合根) 拥有生命周期状态 (`NotProcessed` -> `Processed`)。它是系统流转的核心对象。
    *   **`Topic` / `Subscription`**: 定义了消息路由的元数据。
*   **领域接口 (Domain Interfaces)**:
    *   **`IBrokerService`**: 定义了“消息代理”的能力标准（发消息、收消息、建主题）。这是对基础设施的最高层抽象。
    *   **`IRepository<T>`**: 定义了资源库的标准。
    *   **`IProcessor`**: 定义了业务处理逻辑的标准。
*   **领域服务 (Domain Services)**:
    *   **`Processors/*`** (如 `OrderStreamProcessor`): 封装了特定业务场景下的处理逻辑。例如，如何解析订单数据并推送到第三方。

### 2.2 基础设施层 (Infrastructure Layer)
**项目**: `NEXTEP.MessageBroker.Infrastructure`
*地位：提供技术实现，支撑领域层。*

*   **防腐层 (Anti-Corruption Layer, ACL)**:
    *   **`AwsBrokerService` / `AzureBrokerService`**: 实现了 `IBrokerService`。它们负责将领域层的通用指令翻译成 AWS SDK 或 Azure SDK 的具体调用。这保护了领域层不受云厂商 API 变更的影响。
*   **持久化 (Persistence)**:
    *   通过引用 `NEXTEP.Database.Lib` 实现 `IRepository`，负责将实体状态持久化到 SQL Server。
*   **应用服务/后台任务 (Application Services / Workers)**:
    *   **`MessageBrokerBackgroundService`**: 充当 **Dispatcher**。协调 Repository 和 BrokerService，负责将本地积压的消息推送到云端。
    *   **`WebhookBackgroundService`**: 充当 **Consumer**。协调 BrokerService 和 Processor，负责从云端拉取消息并执行业务处理。

### 2.3 表现层 (Presentation Layer)
**项目**: `NEXTEP.MessageBroker` (Web API)
*地位：系统的门户，负责与外部世界交互。*

*   **Controllers**: `MessagesController` 是 HTTP 请求的入口。
*   **DTOs (Data Transfer Objects)**: 如 `CreateMessageDto`。用于隔离外部 API 契约与内部领域模型。
*   **Mappers**: 负责将 DTO 转换为领域实体 (`Message`)。

### 2.4 共享内核 (Shared Kernel)
**项目**: `NEXTEP.Core.Lib` & `NEXTEP.Database.Lib`
*地位：企业级的通用资产。*

*   包含 `BaseEntity`、通用异常 (`DomainException`)、通用数据库操作封装。这些是所有微服务共享的基础设施。

---

## 3. 核心 DDD 模式应用 (Key Patterns)

### 3.1 依赖倒置 (Dependency Inversion)
*   **现象**: `Infrastructure` 项目引用 `Core` 项目。
*   **意义**: 具体的实现（AWS/Azure）依赖于抽象（`IBrokerService`）。这使得系统可以在不修改核心业务逻辑的情况下切换云厂商。

### 3.2 策略模式 (Strategy Pattern)
*   **代码**: `ProcessorFactory`
*   **意义**: 根据订阅配置动态选择 `IProcessor` 的实现（如 `OrderStreamProcessor` 或 `LunchboxProcessor`）。这符合开闭原则，新增业务类型只需扩展新的 Processor 类。

### 3.3 资源库模式 (Repository Pattern)
*   **代码**: `IRepository<T>`
*   **意义**: 隐藏了数据访问的复杂性（SQL, Dapper, Connection Management），让领域层像操作内存集合一样操作数据库。

---

## 4. 数据流转视图 (Data Flow View)

1.  **入库阶段 (Ingestion)**:
    *   `API` 接收请求 -> 转换为 `Message` 实体 -> `Repository` 落库 (状态: `NotProcessed`)。
    *   *DDD 视角*: 领域对象的创建与持久化。

2.  **分发阶段 (Dispatch)**:
    *   `MessageBrokerBackgroundService` 轮询 DB -> 获取 `Message` 实体 -> 调用 `IBrokerService` 推送云端 -> 更新实体状态 (`Processed`)。
    *   *DDD 视角*: 应用服务协调领域对象与基础设施服务。

3.  **消费阶段 (Consumption)**:
    *   `WebhookBackgroundService` 获取订阅 -> 调用 `IBrokerService` 拉取消息 -> `ProcessorFactory` 创建处理器 -> 执行业务逻辑。
    *   *DDD 视角*: 领域服务的执行与外部系统的交互。

---

## 5. 项目结构与 DDD 层级映射 (Project Structure Mapping)

本节详细展示了代码文件夹结构与 DDD 概念的物理对应关系。

### 5.1 表现层 (Presentation Layer)
**物理项目**: `NEXTEP.MessageBroker` (Web API)
**职责**: 系统的“前台”，负责接待 HTTP 请求，不处理核心业务。

| 文件夹/文件 | DDD 角色 | 详细说明 |
| :--- | :--- | :--- |
| `Controllers/` | **Interface Adapter** | 如 `MessagesController.cs`。它接收外部的 JSON 数据，调用下层服务，并返回 HTTP 200/400/500。它不包含业务逻辑。 |
| `Models/` (DTOs) | **DTO** | 如 `CreateMessageDto`。这是专门给 API 用的数据结构。DDD 强调**隔离**，外部传来的数据结构（DTO）和内部的业务对象（Entity）通常是不一样的，需要转换。 |
| `Program.cs` | **Composition Root** | **组合根**。这是整个系统的装配车间。在这里，通过依赖注入（DI）把 Infrastructure 层的具体实现（如 `AwsBrokerService`）注入到 Core 层的接口（`IBrokerService`）中。 |

### 5.2 领域层 (Domain Layer) —— 核心心脏
**物理项目**: `NEXTEP.MessageBroker.Core`
**职责**: 系统的“大脑”，定义业务规则、实体和标准接口。**它不依赖任何其他项目**。

| 文件夹/文件 | DDD 角色 | 详细说明 |
| :--- | :--- | :--- |
| `Entities/` | **Entity (实体)** | 如 `Message.cs`, `Topic.cs`。这是业务的核心名词。它们拥有唯一的 ID，承载着业务状态（如 `Status`）。 |
| `Interfaces/` | **Domain Interface** | 如 `IBrokerService.cs`, `IRepository.cs`。这是领域层发出的“需求文档”。领域层说：“我需要一个能发消息的服务”，但它不关心具体是谁来实现。 |
| `Processors/` | **Domain Service** | 如 `OrderStreamProcessor.cs`。这是具体的业务逻辑处理单元。当收到消息后，具体怎么解析、怎么映射、怎么推送到第三方，这些逻辑属于业务领域，所以放在这里。 |
| `Mappers/` | **Mapper** | 如 `MessageMapper.cs`。负责把 API 层的 DTO 转换成领域层的 Entity。 |

### 5.3 应用层 (Application Layer) —— 调度指挥
**物理位置**: 混合在 `NEXTEP.MessageBroker.Infrastructure` 和 `NEXTEP.MessageBroker` 中
**职责**: 协调领域对象和基础设施，推动业务流程。

*注意：在这个项目中，应用层没有单独成一个项目，而是通过 **后台服务 (Background Services)** 来体现。*

| 文件夹/文件 | DDD 角色 | 详细说明 |
| :--- | :--- | :--- |
| `Services/MessageBrokerBackgroundService.cs` | **Application Service** | **发件调度员**。它不写业务逻辑，它只是在“搬砖”：从 Repository 取出消息 -> 叫 BrokerService 发送 -> 更新状态。它协调了多个领域对象和服务。 |
| `Services/WebhookBackgroundService.cs` | **Application Service** | **收件调度员**。它负责轮询订阅 -> 拉取消息 -> 找到对应的 Processor 执行。 |

### 5.4 基础设施层 (Infrastructure Layer) —— 技术实现
**物理项目**: `NEXTEP.MessageBroker.Infrastructure`
**职责**: 系统的“手脚”，实现领域层定义的接口，与数据库、云服务交互。

| 文件夹/文件 | DDD 角色 | 详细说明 |
| :--- | :--- | :--- |
| `Services/AwsBrokerService.cs` | **ACL (防腐层)** | 实现了 Core 层的 `IBrokerService`。它把领域层的通用指令翻译成 AWS SDK 的代码。 |
| `Services/AzureBrokerService.cs` | **ACL (防腐层)** | 同上，针对 Azure 的实现。 |
| `Services/DefaultBackgroundTaskQueue.cs` | **Infra Service** | 实现了内存队列的具体逻辑。 |

### 5.5 共享内核 (Shared Kernel)
**物理项目**: `NEXTEP.Core.Lib` 和 `NEXTEP.Database.Lib`
**职责**: 跨微服务的通用资产。

| 组件 | DDD 角色 | 详细说明 |
| :--- | :--- | :--- |
| `BaseEntity` | **Layer Supertype** | 所有实体的父类，统一了 ID 和校验逻辑。 |
| `IRepository<T>` | **Generic Repository** | 定义了通用的增删改查标准。 |
| `NextepDbManager` | **DB Context** | 封装了 Dapper 或 ADO.NET 的底层连接管理。 |

### 5.6 依赖关系图 (Dependency Graph)

```mermaid
graph TD
    %% 定义层级
    subgraph Presentation [表现层: NEXTEP.MessageBroker]
        Controller[MessagesController]
        Program[Program.cs]
    end

    subgraph Domain [领域层: NEXTEP.MessageBroker.Core]
        Entity[Message / Topic]
        Interface[IBrokerService / IRepository]
        Logic[OrderStreamProcessor]
    end

    subgraph Infrastructure [基础设施层: NEXTEP.MessageBroker.Infrastructure]
        Impl[AwsBrokerService / AzureBrokerService]
        Worker[MessageBrokerBackgroundService]
    end

    %% 依赖线条
    Presentation -->|引用| Domain
    Presentation -->|引用| Infrastructure
    
    Infrastructure -->|引用| Domain
    
    %% 逻辑调用线条
    Controller -->|调用| Interface
    Worker -->|调用| Interface
    Worker -->|调用| Logic
    
    %% 实现关系
    Impl ..|>|实现| Interface
```

