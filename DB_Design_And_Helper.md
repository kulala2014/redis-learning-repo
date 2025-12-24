# MessageBroker Database Access Layer Design

本文档梳理了 MessageBroker 项目的数据库访问层（DB Helper）设计模式、核心组件交互逻辑以及参数处理机制。

## 1. 核心架构模式

该项目采用 **基于存储过程的契约模式 (Stored Procedure-driven Contract Pattern)**。这是一种变体的 Repository 模式，强调对存储过程的直接封装和实体的自我管理。

### 核心组件

| 组件层级 | 类/接口示例 | 职责描述 |
| :--- | :--- | :--- |
| **Infrastructure (基建层)** | `BaseContract` | 抽象基类。负责管理数据库连接、事务、Dapper 执行逻辑以及通用的 CRUD 流程。 |
| **Contracts (契约层)** | `MessageContract` | 具体实现类。继承自 `BaseContract`，负责指定具体的存储过程名称（如 `Message_GetList`）。 |
| **Entities (实体层)** | `Message`, `IEntity` | "智能实体"。不仅包含数据，还包含自我填充 (`Load`)、自我参数化 (`GetSaveParameters`) 和自我验证 (`Validate`) 的逻辑。 |
| **Database (数据库层)** | `MessageBroker.DB` | 包含实际 T-SQL 逻辑的存储过程项目。 |

## 2. 数据交互流程

### 2.1 获取数据 (GetListAsync)

数据从数据库流向实体的过程：

1.  **调用**: 业务层调用 `Contract.GetListAsync(criteria)`。
2.  **参数转换**: `BaseContract` 调用 `criteria.GetParameters()` 获取参数字典。
3.  **执行**: 使用 Dapper 执行存储过程 (例如 `Message_GetList`)，获取 `IDataReader`。
4.  **手动映射**:
    *   `BaseContract` 遍历 Reader。
    *   实例化实体 (`new T()`)。
    *   调用实体的 `Load(reader)` 方法。
5.  **填充**: 实体内部通过 `reader["ColumnName"]` 读取数据并赋值给属性。

### 2.2 保存数据 (SaveAsync)

数据从实体流向数据库的过程：

1.  **调用**: 业务层调用 `Contract.SaveAsync(entity)`。
2.  **验证**: 调用 `entity.Validate()` 执行业务规则校验。
3.  **参数化**: 调用 `entity.GetSaveParameters()` 将实体属性转换为参数对象。
4.  **执行**: 执行存储过程 (例如 `Message_Save`)。
5.  **回填**: 存储过程返回更新后的数据（如新 ID），实体通过 `LoadSaveResult(reader)` 更新自身状态。

## 3. Criteria 与参数处理机制

`Criteria` 类是用于构建动态查询条件的核心工具，它与 Dapper 紧密配合实现参数传递。

### 3.1 Criteria 的工作原理

`Criteria` 本质上是一个 **键值对容器 (`Dictionary<string, object>`)** 的封装。

*   **存储**: 使用 `Add(key, value)` 方法将查询条件存入内部字典。
*   **输出**: `GetParameters()` 方法直接返回这个内部字典。

### 3.2 与 Dapper 的配合

当 `BaseContract` 将 `Criteria` 返回的字典传递给 Dapper 时，Dapper 会自动处理映射：

1.  **识别**: Dapper 检测到参数类型为 `IDictionary<string, object>`。
2.  **展开**: 它遍历字典中的每一项。
3.  **映射**:
    *   **Key (字符串)** -> 转换为 SQL 参数名 (例如 `"Status"` -> `@Status`)。
    *   **Value (对象)** -> 转换为 SQL 参数值。
/ 示例：Criteria 内部结构 public object GetParameters() { return this._criteria; // 返回 Dictionary<string, object> }

### 3.3 示例流程

**C# 代码**:
var criteria = new Criteria(); criteria.Add("Status", 1); criteria.Add("City", "New York"); // 传递给 Dapper connection.ExecuteReaderAsync("sp_GetMessages", criteria.GetParameters());

**等效 SQL 执行**:
EXEC sp_GetMessages @Status = 1, @City = 'New York'


## 4. 总结

*   **优点**: 高度封装，性能优异（手动映射避免了反射开销），实体逻辑内聚。
*   **设计哲学**: 数据库逻辑（存储过程）与 C# 逻辑（Contract）强绑定，通过 `Criteria` 提供灵活的参数传递接口。
  
 ## 5. NEXTEP.DATABASE.Lib
### 5.1 ContractRepository.cs 
 
