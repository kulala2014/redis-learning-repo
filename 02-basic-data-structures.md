# 2. Redis 基础数据结构实战

Redis 不仅仅是一个 Key-Value 存储，它支持多种数据结构。以下是 .NET 中的常用操作映射。

## 1. String (字符串)

最基本的数据类型，可以存储字符串、数字（作为字符串存储）。

### 🏗️ 实际项目场景
*   **缓存 (Cache)**: 缓存用户信息、商品详情、配置项等（通常序列化为 JSON 字符串）。
*   **计数器 (Counter)**: 统计网页访问量 (PV)、视频播放量、点赞数 (`INCR`, `INCRBY`)。
*   **分布式锁 (Distributed Lock)**: 利用 `SETNX` (Set if Not Exists) 实现跨进程/跨服务器的互斥锁。
*   **Session 共享**: 在分布式 Web 应用中存储用户的 Session 数据。
*   **限流 (Rate Limiting)**: 利用 `INCR` 和过期时间限制用户在单位时间内的请求次数。
*   **位图 (Bitmap)**: 存储用户签到、在线状态等海量布尔值数据。

> **Redis 知识点详解**：
> *   **底层编码 (Encoding)**：
>     *   **int**: 如果字符串是整数且在 Long 范围内，Redis 会直接存储为整数，节省内存。
>     *   **embstr**: 小于 44 字节的短字符串，SDS 和 RedisObject 连续分配内存，减少内存碎片和分配次数。
>     *   **raw**: 大于 44 字节的长字符串，SDS 独立分配内存。
> *   **SDS (Simple Dynamic String)**：
>     *   **O(1) 获取长度**：头部记录了 len 属性。
>     *   **杜绝缓冲区溢出**：修改前会检查空间，不够自动扩容。
>     *   **空间预分配**：扩容时会多分配一些空间（小于 1M 翻倍，大于 1M 增加 1M），减少连续追加时的内存重分配次数。
>     *   **二进制安全**：不是以 `\0` 判断结尾，而是以 len 属性，所以可以存图片、音频等二进制数据。
> *   **Bitmap**: String 还可以看作是一个 Bit 数组，支持 `SETBIT`, `GETBIT`, `BITCOUNT`，非常适合存储用户签到、活跃状态（1 bit 代表 1 天），极其节省空间。

```csharp
// 设置值
await db.StringSetAsync("username:1001", "zhangsan");


// 设置过期时间 (TTL)
await db.StringSetAsync("otp:123456", "8888", TimeSpan.FromMinutes(5));

// 获取值
string value = await db.StringGetAsync("username:1001");

// 原子递增 (计数器)
// 即使多个客户端同时操作，也能保证计数的准确性
long newCount = await db.StringIncrementAsync("page_view:home");

// 原子递减
long decrCount = await db.StringDecrementAsync("page_view:home");

// 批量操作 (MSET/MGET) - 减少网络往返
await db.StringSetAsync(new KeyValuePair<RedisKey, RedisValue>[] {
    new("key1", "value1"),
    new("key2", "value2")
});
RedisValue[] values = await db.StringGetAsync(new RedisKey[] { "key1", "key2" });

// GetSet (设置新值并返回旧值) - 常用于实现锁的续期或状态切换
RedisValue oldValue = await db.StringGetSetAsync("key1", "new_value");

// Append (追加字符串)
await db.StringAppendAsync("key1", "_suffix");

// GetRange (获取子字符串)
// 对应命令: GETRANGE key start end
string? subStr = await db.StringGetRangeAsync("key1", 0, 5);

// SetRange (覆盖子字符串)
// 对应命令: SETRANGE key offset value
await db.StringSetRangeAsync("key1", 6, " Redis");

// StringLength (获取字符串长度)
// 对应命令: STRLEN key
long len = await db.StringLengthAsync("key1");

// IncrementByFloat (浮点数增减)
// 对应命令: INCRBYFLOAT key increment
double newFloat = await db.StringIncrementAsync("float_key", 2.5);
await db.StringIncrementAsync("float_key", -1.5); // 减法

// PSETEX (毫秒级过期)
// 对应命令: PSETEX key milliseconds value
await db.StringSetAsync("fast_key", "val", TimeSpan.FromMilliseconds(1500));

// MSETNX (批量设置，仅当所有 key 都不存在时成功)
// 对应命令: MSETNX key value [key value ...]
// 注意：这是原子操作，要么全成功，要么全失败
bool msetnx = await db.StringSetAsync(new KeyValuePair<RedisKey, RedisValue>[] {
    new("new_k1", "v1"),
    new("new_k2", "v2")
}, When.NotExists);

// SETNX (Set if Not Exists - 分布式锁的基础)
// 只有当 key 不存在时才设置成功
// ⚠️ 关键点：必须设置过期时间 (TimeSpan)，防止客户端崩溃导致死锁
bool wasSet = await db.StringSetAsync("lock:resource", "holder_id", TimeSpan.FromSeconds(10), When.NotExists);
if (wasSet)
{
    try 
    {
        Console.WriteLine("Lock acquired!");
        // 执行业务逻辑...
    }
    finally 
    {
        // 释放锁 (简单版：直接删除)
        // 严谨版：应该先检查 Value 是否是自己的 holder_id 再删除 (使用 Lua 脚本)
        await db.KeyDeleteAsync("lock:resource");
    }
}
else
{
    Console.WriteLine("Lock already exists.");
}



// Bitmap 操作 (位图)
// 原理：String 本质是字节数组，可以按 Bit (位) 进行操作。
// 场景：用户签到、活跃统计。极其节省空间 (1 亿用户每天签到只需要 ~12MB)。
string bitmapKey = "user:sign:2023:10";

// SETBIT key offset value
// offset: 偏移量 (从 0 开始)。例如用日期作为 offset。
// 意思：将 bitmapKey 对应的二进制数据的第 1 位设置为 1。
await db.StringSetBitAsync(bitmapKey, 1, true); // 1号签到
await db.StringSetBitAsync(bitmapKey, 5, true); // 5号签到

// GETBIT key offset
// 意思：获取 bitmapKey 对应的二进制数据的第 1 位的值 (0 或 1)。
bool isSignedDay1 = await db.StringGetBitAsync(bitmapKey, 1); // true
bool isSignedDay2 = await db.StringGetBitAsync(bitmapKey, 2); // false

// BITCOUNT
// 统计所有位中 1 的个数 (本月总签到天数)
long totalSignDays = await db.StringBitCountAsync(bitmapKey);



```

## 2. Hash (哈希)

类似于 .NET 中的 `Dictionary<string, string>` 或对象。适合存储对象的各个字段。

### 🏗️ 实际项目场景
*   **对象存储**: 存储用户资料（姓名、年龄、积分）、商品信息（价格、库存、描述）。相比 String 存储 JSON，Hash 可以只修改其中一个字段（如只修改库存），避免并发覆盖问题。
*   **购物车**: Key 为用户 ID，Field 为商品 ID，Value 为购买数量。
*   **配置中心**: 存储系统的运行时配置，支持单个配置项的动态更新。

> **Redis 知识点详解**：
> *   **底层编码 (Encoding)**：
>     *   **ziplist (压缩列表)**：当 Hash 元素个数少（默认 < 512）且所有值都小（默认 < 64字节）时使用。它是一块连续的内存，紧凑存储，没有指针开销，但查询需要遍历 O(N)。
>     *   **hashtable (哈希表)**：当不满足 ziplist 条件时，自动转换为 hashtable。查询复杂度 O(1)。
> *   **渐进式 Rehash**：当 Hash 表扩容或缩容时，Redis 不是一次性搬迁所有数据（会阻塞主线程），而是分多次、渐进式地将旧表数据迁移到新表。
> *   **内存效率**：Hash 结构非常适合存储对象。相比于将对象序列化为 JSON String 存储，Hash 可以单独读写字段，且在 ziplist 模式下极其节省内存。

```csharp
string key = "user:1001";


// 存储对象字段
await db.HashSetAsync(key, new HashEntry[] {
    new HashEntry("name", "John"),
    new HashEntry("age", 30),
    new HashEntry("city", "New York")
});

// 获取单个字段
string name = await db.HashGetAsync(key, "name");

// 获取所有字段
HashEntry[] allEntries = await db.HashGetAllAsync(key);

// 获取所有 Key (HKEYS)
RedisValue[] keys = await db.HashKeysAsync(key);

// 获取所有 Value (HVALS)
RedisValue[] vals = await db.HashValuesAsync(key);

// 判断字段是否存在 (HEXISTS)
bool hasAge = await db.HashExistsAsync(key, "age");

// 删除字段 (HDEL)
bool deleted = await db.HashDeleteAsync(key, "city");

// 字段值自增 (HINCRBY)
long newAge = await db.HashIncrementAsync(key, "age", 1); // 年龄+1

// 字段值浮点自增 (HINCRBYFLOAT)
double newBalance = await db.HashIncrementAsync(key, "balance", 5.7);

// 获取多个字段 (HMGET)
RedisValue[] multiFields = await db.HashGetAsync(key, new RedisValue[] { "name", "age" });

// 获取字段数量 (HLEN)
long hashLen = await db.HashLengthAsync(key);

// 获取字段值的字符串长度 (HSTRLEN)
long valLen = await db.HashStringLengthAsync(key, "name");

// 仅当字段不存在时设置 (HSETNX)
bool setNx = await db.HashSetAsync(key, "nickname", "Clyde", When.NotExists);

// HSCAN (迭代获取字段)
// 场景：当 Hash 非常大时，使用 HashGetAll 会阻塞 Redis。
// 此时应该使用 HashScan 进行渐进式遍历。
// 参数：
//   pattern: 匹配模式 (如 "user*")
//   pageSize: 每次迭代返回的数量 (近似值)
foreach (var entry in db.HashScan(key, "name*", pageSize: 10))
{
    Console.WriteLine($"Found field: {entry.Name} = {entry.Value}");
}


```

## 3. List (列表)

双向链表。支持从两端推入或弹出元素。

### 🏗️ 实际项目场景
*   **消息队列 (Message Queue)**: 简单的生产者-消费者模型 (`LPUSH` + `BRPOP`)，用于异步处理任务（如发送邮件、后台报表生成）。
*   **最新列表 (Timeline)**: 存储用户的最新动态、最新的 N 条新闻、评论列表（利用 `LPUSH` + `LTRIM` 保持固定长度）。
*   **朋友圈/关注列表**: 存储用户关注的人发布的动态 ID 列表。

> **Redis 知识点详解**：
> *   **底层编码 (Encoding)**：
>     *   **quicklist (快速列表)**：Redis 3.2 之后引入。它是一个双向链表，但每个节点不是存一个元素，而是一个 **ziplist**。
>     *   **优势**：结合了双向链表（插入删除 O(1)）和 ziplist（内存紧凑、无指针碎片）的优点。
> *   **阻塞操作**：List 支持 `BLPOP` / `BRPOP` 命令。如果 List 为空，客户端会阻塞等待，直到有新元素被 Push 进来。这是实现**消息队列**的关键特性。
> *   **性能注意**：
>     *   `LPUSH`/`RPOP` 是 O(1)。
>     *   `LINDEX` (按索引取值) 是 O(N)，如果 List 很长，尽量避免随机访问。

```csharp
string listKey = "task_queue";


// 生产者：推入队列
await db.ListRightPushAsync(listKey, "task_1");
await db.ListRightPushAsync(listKey, "task_2");

// 消费者：从左侧弹出 (阻塞式或非阻塞式)
string task = await db.ListLeftPopAsync(listKey);

// 消费者：从右侧弹出 (RPOP)
string rightTask = await db.ListRightPopAsync(listKey);

// 获取列表长度
long len = await db.ListLengthAsync(listKey);

// 批量 Push (LPUSH)
await db.ListLeftPushAsync(listKey, new RedisValue[] { "Item A", "Item B", "Item C" });

// LPUSHX (仅当列表存在时才插入)
await db.ListLeftPushAsync(listKey, "Item X", When.Exists);

// 获取范围 (分页)
RedisValue[] page = await db.ListRangeAsync(listKey, 0, 9);

// 裁剪列表 (LTRIM) - 只保留指定范围，常用于保持固定长度的日志
await db.ListTrimAsync(listKey, 0, 99); // 只保留最新的 100 条

// 移除元素 (LREM) - 移除前 2 个值为 "task_1" 的元素
await db.ListRemoveAsync(listKey, "task_1", 2);

// 获取指定索引元素 (LINDEX)
RedisValue item = await db.ListGetByIndexAsync(listKey, 0);

// 按索引设置值 (LSET)
await db.ListSetByIndexAsync(listKey, 1, "Updated Item");

// 插入元素 (LINSERT) - 在 "task_2" 之前插入 "task_1.5"
await db.ListInsertBeforeAsync(listKey, "task_2", "task_1.5");

// 插入元素 (LINSERT) - 在 "task_2" 之后插入 "task_2.5"
await db.ListInsertAfterAsync(listKey, "task_2", "task_2.5");


// RPOPLPUSH (可靠队列模式)
// 对应命令: RPOPLPUSH source destination
// 作用：原子性地从 source 右侧弹出一个元素，并推入 destination 左侧。
// 场景：任务处理。从 "待处理" 移动到 "处理中"，防止消费者崩溃导致任务丢失。
RedisValue workItem = await db.ListRightPopLeftPushAsync(listKey, "processing_list");

// 关于 BRPOPLPUSH (阻塞式 RPOPLPUSH):
// 对应命令: BRPOPLPUSH source destination timeout
// 注意：StackExchange.Redis 基于多路复用 (Multiplexer) 设计，默认不支持在共享连接上使用阻塞命令 (BLPOP, BRPOPLPUSH)，
// 因为这会阻塞该连接上的所有其他并发请求。
// 解决方案：
// 1. 使用非阻塞的 ListRightPopLeftPushAsync 配合轮询。
// 2. 使用 Redis Streams (推荐用于复杂队列)。
// 3. 如果必须使用，需要创建一个独立的 ConnectionMultiplexer 实例专门用于阻塞操作。


```

## 4. Set (集合)

无序的字符串集合，自动去重。

### 🏗️ 实际项目场景
*   **标签系统 (Tags)**: 存储用户的兴趣标签、商品的分类标签。
*   **社交关系**: 存储好友列表、粉丝列表、关注列表。
    *   **共同好友**: 利用 `SINTER` 计算两个用户的交集。
    *   **可能认识的人**: 利用 `SDIFF` 计算差集。
*   **去重统计 (UV)**: 统计网站的独立访客 (Unique Visitor)，利用 Set 的自动去重特性。
*   **抽奖系统**: 利用 `SRANDMEMBER` 或 `SPOP` 随机抽取幸运用户。
*   **黑白名单**: 快速判断某个 IP 或用户 ID 是否在黑名单中 (`SISMEMBER`)。

> **Redis 知识点详解**：
> *   **底层编码 (Encoding)**：
>     *   **intset (整数集合)**：当集合元素全是整数，且数量较少（默认 < 512）时使用。底层是有序数组，使用二分查找判断元素是否存在，复杂度 O(log N)。
>     *   **hashtable**：当元素包含字符串或数量较多时，转换为 hashtable。判断元素是否存在复杂度 O(1)。
> *   **应用场景扩展**：
>     *   **抽奖**：`SRANDMEMBER` 随机获取元素。
>     *   **社交**：`SINTER` 计算共同好友。
> *   **性能注意**：`SMEMBERS` 会返回所有元素，如果集合很大（如几百万），会阻塞 Redis。生产环境建议使用 `SSCAN` 迭代获取。

```csharp
string setKey = "unique_visitors:2023-10-27";


// 添加元素 (自动去重)
await db.SetAddAsync(setKey, "192.168.1.1");
await db.SetAddAsync(setKey, "192.168.1.1"); // 不会重复添加

// 判断是否存在 (SISMEMBER)
bool isMember = await db.SetContainsAsync(setKey, "192.168.1.1");

// 获取集合大小 (SCARD)
long count = await db.SetLengthAsync(setKey);

// 获取所有元素 (SMEMBERS)
// 注意：如果集合很大，请慎用，改用 SetScan
RedisValue[] members = await db.SetMembersAsync(setKey);

// 移除元素 (SREM)
await db.SetRemoveAsync(setKey, "192.168.1.1");

// 随机获取元素 (SRANDMEMBER) - 适合抽奖
RedisValue randomMember = await db.SetRandomMemberAsync(setKey);

// 随机弹出元素 (SPOP) - 获取并删除
RedisValue poppedMember = await db.SetPopAsync(setKey);

// 集合运算
string setA = "set:a";
string setB = "set:b";

// 交集 (SINTER) - 共同好友
RedisValue[] intersect = await db.SetCombineAsync(SetOperation.Intersect, setA, setB);

// 并集 (SUNION) - 所有好友
RedisValue[] union = await db.SetCombineAsync(SetOperation.Union, setA, setB);

// 差集 (SDIFF) - A 有 B 没有
RedisValue[] diff = await db.SetCombineAsync(SetOperation.Difference, setA, setB);

// 集合运算并存储 (SDIFFSTORE / SUNIONSTORE / SINTERSTORE)
// 将 setA 和 setB 的差集存储到 "set:diff" 中
await db.SetCombineAndStoreAsync(SetOperation.Difference, "set:diff", setA, setB);

// 移动元素 (SMOVE) - 将元素从 A 移动到 B
bool moved = await db.SetMoveAsync(setA, setB, "member1");

// 迭代获取元素 (SSCAN)
// 避免 SMEMBERS 阻塞 Redis
foreach (var member in db.SetScan(setKey, "ip_*", pageSize: 10))
{
    Console.WriteLine($"Found: {member}");
}

```

## 5. Sorted Set (有序集合)

类似于 Set，但每个元素关联一个分数 (Score)，按分数排序。

### 🏗️ 实际项目场景
*   **排行榜 (Leaderboard)**: 游戏积分排行榜、视频热度排行榜、直播间贡献榜（Score 为分数/热度）。
*   **延迟队列 (Delay Queue)**: 存储需要延迟执行的任务，Score 为任务执行的时间戳。消费者轮询 `ZRANGEBYSCORE` 获取到期的任务。
*   **带权重的消息队列**: 优先级高的任务 Score 较小/大，优先被处理。
*   **时间轴/动态流**: 存储按时间排序的动态，Score 为发布时间戳，支持按时间范围拉取。

> **Redis 知识点详解**：
> *   **底层编码 (Encoding)**：
>     *   **ziplist**：元素少且小的时候使用。
>     *   **skiplist (跳表)**：核心数据结构。
>         *   它是一种随机化的数据结构，通过在链表上增加多级索引（Level），实现快速查找。
>         *   **查找/插入/删除** 平均复杂度均为 **O(log N)**。
>         *   相比红黑树/平衡树，跳表实现简单，且并发支持更好（虽然 Redis 是单线程，但跳表在范围查找 Range Query 上性能更优）。
>     *   同时维护了一个 **Hashtable**，用于 O(1) 获取成员的分数 (Score)。
> *   **Score 精度**：Score 是双精度浮点数 (double)，如果用于存储高精度时间戳或金额，要注意浮点数精度问题。

```csharp
string lbKey = "game_leaderboard";


// 添加元素及其分数
await db.SortedSetAddAsync(lbKey, "PlayerA", 100);
await db.SortedSetAddAsync(lbKey, "PlayerB", 250);
await db.SortedSetAddAsync(lbKey, "PlayerC", 50);

// 获取前 10 名 (按分数从高到低)
var top10 = await db.SortedSetRangeByRankWithScoresAsync(lbKey, 0, 9, Order.Descending);

foreach (var item in top10)
{
    Console.WriteLine($"{item.Element}: {item.Score}");
}

// 增加分数 (ZINCRBY)
double newScore = await db.SortedSetIncrementAsync(lbKey, "PlayerA", 50);

// 获取分数 (ZSCORE)
double? score = await db.SortedSetScoreAsync(lbKey, "PlayerA");

// 获取排名 (ZRANK) - 从 0 开始，低分在前
long? rank = await db.SortedSetRankAsync(lbKey, "PlayerA");

// 获取倒序排名 (ZREVRANK) - 高分在前 (通常用于排行榜)
long? revRank = await db.SortedSetRankAsync(lbKey, "PlayerA", Order.Descending);

// 获取集合成员数量 (ZCARD)
long count = await db.SortedSetLengthAsync(lbKey);

// 获取指定分数范围的数量 (ZCOUNT)
long countInRange = await db.SortedSetLengthAsync(lbKey, 100, 200);

// 按分数范围获取成员 (ZRANGEBYSCORE)
var midPlayers = await db.SortedSetRangeByScoreWithScoresAsync(lbKey, 100, 200);

// 移除元素 (ZREM)
await db.SortedSetRemoveAsync(lbKey, "PlayerC");

// 按排名移除 (ZREMRANGEBYRANK) - 移除最后一名
await db.SortedSetRemoveRangeByRankAsync(lbKey, 0, 0);

// 按分数移除 (ZREMRANGEBYSCORE) - 移除分数低于 60 的
await db.SortedSetRemoveRangeByScoreAsync(lbKey, 0, 60);

// 集合交集运算并存储 (ZINTERSTORE)
// 计算 lbKey 和 anotherKey 的交集，存入 destKey
await db.SortedSetCombineAndStoreAsync(SetOperation.Intersect, "destKey", lbKey, "anotherKey");

// 字典序范围查询 (ZRANGEBYLEX) - 仅当所有成员分数相同时有效
// 获取 PlayerA 到 PlayerZ 之间的成员
var lexMembers = await db.SortedSetRangeByValueAsync(lbKey, "PlayerA", "PlayerZ");

// 迭代获取成员 (ZSCAN)
foreach (var entry in db.SortedSetScan(lbKey, "*", pageSize: 10))
{
    Console.WriteLine($"{entry.Element}: {entry.Score}");
}

```
