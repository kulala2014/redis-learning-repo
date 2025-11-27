using StackExchange.Redis;

// 连接到 Redis
// 默认连接到 localhost:6379
var redis = ConnectionMultiplexer.Connect("localhost");
var db = redis.GetDatabase();

Console.WriteLine("Connected to Redis");

// 1. 基础 String 操作
Console.WriteLine("\n--- String Operations ---");
string key = "my_key";
string value = "Hello Redis from .NET";

// Set
await db.StringSetAsync(key, value);
Console.WriteLine($"Set {key} = {value}");

// Set another 
await db.StringSetAsync("kulala1", value);
Console.WriteLine($"Set kulala1 = {value}");

// Get
string? retrievedValue = await db.StringGetAsync(key);
Console.WriteLine($"Get {key} = {retrievedValue}");

// GetRange
string? strRangeValue = await db.StringGetRangeAsync(key, 0, 5);
Console.WriteLine($"Get Range {key} = {strRangeValue}");

//GetSet 
string? oldValue = await db.StringGetSetAsync(key, "Kulala set New Value");
Console.WriteLine($"GetSet {key}, old value = {oldValue}, new value = Kulala set New Value");

//GETBIT means  
var bitValue = await db.StringGetBitAsync(key, 2);
Console.WriteLine($"GetBit {key} at position 2 = {bitValue}");

//MGET
var mgetValues = await db.StringGetAsync(new RedisKey[] { key, "kulala1" });
Console.WriteLine($"MGet values: {string.Join(", ", mgetValues)}");

//SETBIT
bool setBitResult = await db.StringSetBitAsync(key, 2, true);
Console.WriteLine($"SetBit {key} at position 2 to true: {setBitResult}");

// SETNX (Set if Not Exists - 分布式锁的基础)
// 只有当 key 不存在时才设置成功
Console.WriteLine("Trying SETNX on 'my_key' (already exists)...");
bool setNxResult1 = await db.StringSetAsync(key, "This is a new key", null,  When.NotExists);
Console.WriteLine($"SETNX result: {setNxResult1} (Should be False)");

Console.WriteLine("Trying SETNX on 'new_unique_key' (does not exist)...");
// 最佳实践：SETNX 必须带过期时间，防止死锁
bool setNxResult2 = await db.StringSetAsync("new_unique_key", "unique value", TimeSpan.FromSeconds(10), When.NotExists);

if (setNxResult2)
{
    Console.WriteLine("Lock acquired!");
    // 模拟业务处理
    await Task.Delay(100);
    // 释放锁
    await db.KeyDeleteAsync("new_unique_key");
}
else
{
    Console.WriteLine("Lock already exists.");
}


// SETEX Expiration (设置带过期时间的键值):  SETEX key seconds value
await db.StringSetAsync("temp_key", "I will disappear in 5 seconds", TimeSpan.FromSeconds(5));
Console.WriteLine("Set temp_key with 5 seconds expiration");


//SETRANGE: Overwrite part of a string at key starting at the specified offset
await db.StringSetRangeAsync(key, 6, " Redis");
string? setRangeValue = await db.StringGetAsync(key);
Console.WriteLine($"After SetRange, {key} = {setRangeValue}");

//STRLEN: Get the length of the string value stored at key
long strLen = await db.StringLengthAsync(key);
Console.WriteLine($"Length of {key} = {strLen}");

//MSET: 同时设置多个键值对
await db.StringSetAsync(new KeyValuePair<RedisKey, RedisValue>[] {
    new KeyValuePair<RedisKey, RedisValue>("key1", "value1"),
    new KeyValuePair<RedisKey, RedisValue>("key2", "value2"),
    new KeyValuePair<RedisKey, RedisValue>("key3", "value3"),
});
var msetValues = await db.StringGetAsync(new RedisKey[] {"key1","key2", "key3" });
Console.WriteLine("MSET key1, key2, key3", string.Join(',', msetValues));

//MSETNX: 同时设置多个键值对，只有当所有键都不存在时才成功

bool msetnxResult = await db.StringSetAsync(new KeyValuePair<RedisKey, RedisValue>[] {
    new KeyValuePair<RedisKey, RedisValue>("key3", "value3"),
    new KeyValuePair<RedisKey, RedisValue>("key5", "value5"),
    }, When.NotExists);

Console.WriteLine($"MSETNX result: {msetnxResult} (Should be False because key3 exists)");

//PSETEX:PSETEX key milliseconds value: 设置带过期时间的键值，过期时间单位为毫秒
await db.StringSetAsync("psetex_key", "I expire in 1500 ms", TimeSpan.FromMilliseconds(1500));
Console.WriteLine("Set psetex_key with 1500 ms expiration");

// Increment
await db.StringSetAsync("counter", 0);
long newValue = await db.StringIncrementAsync("counter");
Console.WriteLine($"Counter incremented to: {newValue}");

//increment by float
double newFloatValue = await db.StringIncrementAsync("float_counter", 2.5);
Console.WriteLine($"Float counter incremented to: {newFloatValue}");

//Increment by float negative
double newFloatValueNeg = await db.StringIncrementAsync("float_counter", -1.5);
Console.WriteLine($"Float counter decremented to: {newFloatValueNeg}");

//INCREMENTBY
long incrementedBy5 = await db.StringIncrementAsync("counter", 5);
Console.WriteLine($"Counter incremented by 5 to: {incrementedBy5}");

//DECREMENT
long decrementedValue = await db.StringDecrementAsync("counter");
Console.WriteLine($"Counter decremented to: {decrementedValue}");
//DECREMENTBY
long decrementedBy3 = await db.StringDecrementAsync("counter", 3);
Console.WriteLine($"Counter decremented by 3 to: {decrementedBy3}");

//DECRMENTBYFLOAT
double decrementedFloat = await db.StringDecrementAsync("float_counter", 0.5);
Console.WriteLine($"Float counter decremented by 0.5 to: {decrementedFloat}");

//APPEND:如果 key 已经存在并且是一个字符串， APPEND 命令将指定的 value 追加到该 key 原来值（value）的末尾。
long appendLength = await db.StringAppendAsync(key, " - Appended Text");
Console.WriteLine($"After Append, length of {key} = {appendLength}");

// SETNX (Set if Not Exists)
Console.WriteLine("Trying SETNX on 'my_key' (already exists)...");
bool setnxResult1 = await db.StringSetAsync(key, "New Value", null, When.NotExists);
Console.WriteLine($"SETNX result: {setnxResult1} (Should be False)");

Console.WriteLine("Trying SETNX on 'new_unique_key' (does not exist)...");
bool setnxResult2 = await db.StringSetAsync("new_unique_key", "Unique Value", null, When.NotExists);
Console.WriteLine($"SETNX result: {setnxResult2} (Should be True)");

// Bitmap 操作
Console.WriteLine("\n--- Bitmap Operations ---");
string bitmapKey = "user:sign:2023:11";
// 模拟签到：1号和5号签到
await db.StringSetBitAsync(bitmapKey, 1, true);
await db.StringSetBitAsync(bitmapKey, 5, true);
Console.WriteLine("User signed in on day 1 and 5.");

var result12 = await db.StringGetAsync(bitmapKey);
Console.WriteLine($"Bitmap value: {result12}");

// 检查签到状态
bool isSignedDay1 = await db.StringGetBitAsync(bitmapKey, 1);
bool isSignedDay2 = await db.StringGetBitAsync(bitmapKey, 2);
Console.WriteLine($"Is signed on day 1? {isSignedDay1}");
Console.WriteLine($"Is signed on day 2? {isSignedDay2}");

// 统计签到天数
long totalSignDays = await db.StringBitCountAsync(bitmapKey);
Console.WriteLine($"Total sign-in days: {totalSignDays}");

// 2. List 操作 (列表)
//Redis列表是简单的字符串列表，按照插入顺序排序。你可以添加一个元素到列表的头部（左边）或者尾部（右边）
//一个列表最多可以包含 232 - 1 个元素 (4294967295, 每个列表超过40亿个元素)。
Console.WriteLine("\n--- List Operations ---");


string listKey = "my_list";
await db.KeyDeleteAsync(listKey); // 清理旧数据

// Push
await db.ListRightPushAsync(listKey, "Item 1");
await db.ListRightPushAsync(listKey, "Item 2");
await db.ListLeftPushAsync(listKey, "Item 0"); // 从左侧插入

// Range
var listItems = await db.ListRangeAsync(listKey);
Console.WriteLine($"List items: {string.Join(", ", listItems)}");

// PopLeft
var poppedItem = await db.ListLeftPopAsync(listKey);
Console.WriteLine($"Popped from left: {poppedItem}");

//PopRight
var listItems1 = await db.ListRangeAsync(listKey);
Console.WriteLine($"List items: {string.Join(", ", listItems1)}");
var popedRightItem = await db.ListRightPopAsync(listKey);
Console.WriteLine($"Popped from right: {popedRightItem}");


var new_list_key = "desd_list";
//LEFT POP RIGHT PUSH
var lprpItem = await db.ListRightPopLeftPushAsync(listKey, new_list_key);
Console.WriteLine($"Left Pop Right Push item: {lprpItem}");

//LINDEX
var indexItem = await db.ListGetByIndexAsync(new_list_key, 0);
Console.WriteLine($"Item at index 0 in {new_list_key}: {indexItem}");

//LLEN
long listLength = await db.ListLengthAsync(new_list_key);
Console.WriteLine($"Length of {new_list_key}: {listLength}");

//lpush multiple items
await db.ListLeftPushAsync(new_list_key, new RedisValue[] { "Item A", "Item B", "Item C" });
//LRANGE
var rangeItems = await db.ListRangeAsync(new_list_key, 0, -1);
Console.WriteLine($"All items in {new_list_key}: {string.Join(", ", rangeItems)}");

//LPUSHX
// 只有当列表存在时才插入
await db.ListLeftPushAsync(new_list_key, "Item X", When.Exists);

//LREM
// 从列表中删除指定值的元素
long removedCount = await db.ListRemoveAsync(new_list_key, "Item B", 1); // 删除第一个匹配的

//lset with index
await db.ListSetByIndexAsync(new_list_key, 1, "Updated Item");

//LTRIM
// 裁剪列表，只保留指定范围内的元素
await db.ListTrimAsync(new_list_key, 0, 2); // 只保留前3个元素
var llenItems = await db.ListRangeAsync(new_list_key, 0, -1);
Console.WriteLine($"Items after LTRIM in {new_list_key}: {string.Join(", ", llenItems)}");


//LINSERT
// 在列表中找到这个值 (indexItem)，并在它后面插入新值
// 参数 1: Key
// 参数 2: Pivot (基准值) - Redis 会在列表中从左往右查找这个值
// 参数 3: Value (新值) - 要插入的数据
await db.ListInsertAfterAsync(new_list_key, indexItem!, "Inserted Item");


// 3. Hash 操作 (哈希)
//Redis 的 Hash 类型和 Python 的 dict（字典）非常相似。

//都是键值对集合，支持按 key 存取、修改、删除字段。
//都可以高效地存储和读取单个字段，也可以一次性获取所有字段。
//Redis Hash 适合存储对象的属性，比如用户信息、商品详情等。
//区别：

//Redis 的 Hash 存储在服务端，可以被多个客户端并发访问。
//Python 的 dict 是本地内存结构，只能在单个进程内使用。
//你可以把 Redis Hash 理解为“存储在 Redis 里的远程字典”。
Console.WriteLine("\n--- Hash Operations ---");
string hashKey = "user:1001";

// Set Hash entries
await db.HashSetAsync(hashKey, new HashEntry[] {
    new HashEntry("name", "John Doe"),
    new HashEntry("age", 30),
    new HashEntry("email", "john@example.com")
});

// Get Hash entry
string? name = await db.HashGetAsync(hashKey, "name");
Console.WriteLine($"User Name: {name}");

// Get All Hash entries
var allEntries = await db.HashGetAllAsync(hashKey);
Console.WriteLine("All User Properties:");
foreach (var entry in allEntries)
{
    Console.WriteLine($"  {entry.Name}: {entry.Value}");
}

//
string hashKey2 = "user:1002";

//SetHash entry
await db.HashSetAsync(hashKey2, new HashEntry[] {
    new HashEntry("name", "Jane Smith"),
    new HashEntry("age", 25),
    new HashEntry("email", "clyde.gao@live.cn"),
    new HashEntry("gender", "male"),
    new HashEntry("fullName", "clyde.gao"),
    new HashEntry("balance", "10.3"),
    });
//Get Hash entry
string? email = await db.HashGetAsync(hashKey2, "email");
Console.WriteLine($"User Email: {email}");
//Get All Hash entries
var allEntries2 = await db.HashGetAllAsync(hashKey2);
Console.WriteLine("All User Properties for user:1002:");
foreach (var entry in allEntries2)
{
    Console.WriteLine($"  {entry.Name}: {entry.Value}");
}

//HDEL
bool deleted = await db.HashDeleteAsync(hashKey2, "fullName");
Console.WriteLine($"Deleted fullName from {hashKey2}: {deleted}");
//HEXISTS
bool existsHash = await db.HashExistsAsync(hashKey2, "fullName");
Console.WriteLine($"fullName exists in {hashKey2}: {existsHash}");

//HINCRBY KEY FIELD
var age = await db.HashIncrementAsync(hashKey2, "age");
Console.WriteLine($"Incremented age in {hashKey2}: {age}");


//HINCRBY KEY FIELD INCREMENT
var age1 = await db.HashIncrementAsync(hashKey2, "age",3);
Console.WriteLine($"Incremented age in {hashKey2}: {age}");

//INCRBYFLOAT
var balance = await db.HashIncrementAsync(hashKey2, "balance", 5.7);
Console.WriteLine($"Incremented balance in {hashKey2}: {balance}");

//HKEYS GET ALL FIELDS
var hashKeys = await db.HashKeysAsync(hashKey2);
Console.WriteLine($"Keys in {hashKey2}: {string.Join(", ", hashKeys)}");

//HLEN GET NUMBER OF FIELDS
long hashLength = await db.HashLengthAsync(hashKey2);
Console.WriteLine($"Number of fields in {hashKey2}: {hashLength}");

//HMGET GET MULTIPLE FIELDS
var multiFields = await db.HashGetAsync(hashKey2, new RedisValue[] { "name", "email", "age" });
Console.WriteLine($"Multi fields in {hashKey2}: {string.Join(", ", multiFields)}");

//HMSET SET MULTIPLE FIELDS
await db.HashSetAsync(hashKey2, new HashEntry[] {
    new HashEntry("address", "123 Main St"),
    new HashEntry("phone", "555-1234")
    });
var updatedEntries = await db.HashGetAllAsync(hashKey2);
Console.WriteLine("Updated User Properties for user:1002:");
foreach (var entry in updatedEntries)
{
    Console.WriteLine($"  {entry.Name}: {entry.Value}");
}

//HVALS GET ALL VALUES
var hashValues = await db.HashValuesAsync(hashKey2);
foreach (var entry in hashValues)
{
    Console.WriteLine($"Value in {hashKey2}: {entry}");
}

//HSETNX KEY FIELD VALUE
bool hsetResult = await db.HashSetAsync(hashKey2, "nickname", "Clyde", When.NotExists);
Console.WriteLine($"HSETNX result for nickname in {hashKey2}: {hsetResult}");

//HSET KEY FIELD VALUE
bool hsetResult2 = await db.HashSetAsync(hashKey2, "nickname", "Clyde");
Console.WriteLine($"HSET result for nickname in {hashKey2}: {hsetResult2}");

//HSTRLEN KEY FIELD
long hstrlen = await db.HashStringLengthAsync(hashKey2, "nickname");
Console.WriteLine($"HSTRLEN of nickname in {hashKey2}: {hstrlen}");


//HSCAN (遍历哈希表)
// HSCAN Example
Console.WriteLine("\n--- HSCAN Operation ---");
// 插入一些测试数据
await db.HashSetAsync("large_hash", new HashEntry[] {
    new("field1", "value1"),
    new("field2", "value2"),
    new("other_field", "value3"),
    new("field3", "value4")
});

Console.WriteLine("Scanning 'large_hash' for fields starting with 'field'...");
// HashScan 返回的是一个 IEnumerable，内部会自动处理游标 (Cursor)
// 这里的 pageSize 对应命令中的 COUNT
foreach (var entry in db.HashScan("large_hash", "field*", pageSize: 2))
{
    Console.WriteLine($"  Found: {entry.Name} -> {entry.Value}");
}


// 4. Set 操作 (集合 - 无序不重复)
Console.WriteLine("\n--- Set Operations ---");
string setKey = "unique_visitors";
//SADD : Add members to a set
await db.SetAddAsync(setKey, "ip_1");
await db.SetAddAsync(setKey, "ip_2");
await db.SetAddAsync(setKey, "ip_1"); // 重复的不会被添加

long count = await db.SetLengthAsync(setKey);
Console.WriteLine($"Set size: {count} (should be 2)");

//SISMEMBER : Check if a member exists in the set
bool exists = await db.SetContainsAsync(setKey, "ip_1");
Console.WriteLine($"ip_1 exists: {exists}");

string setKey1 = "unique_visitors1";
await db.SetAddAsync(setKey1, "ip_1");
await db.SetAddAsync(setKey1, "ip_2");
await db.SetAddAsync(setKey1, "ip_3");
await db.SetAddAsync(setKey1, "ip_4");
await db.SetAddAsync(setKey1, "ip_4");

//SCARD : Get the number of members in a set
long setCount = await db.SetLengthAsync(setKey1);
Console.WriteLine($"Set size of {setKey1}: {setCount} (should be 4)");

//SMEMBERS: Get all members of the set
var setMembers = await db.SetMembersAsync(setKey1);
Console.WriteLine($"Members of {setKey1}: {string.Join(", ", setMembers)}");

//SISMEMBER : Check if a member exists in the set
bool isMember = await db.SetContainsAsync(setKey1, "ip_3");
Console.WriteLine($"ip_3 is member of {setKey1}: {isMember}");

//SETDIFF : Get the difference between two sets
var diffMembers = await db.SetCombineAsync(SetOperation.Difference, setKey1, setKey);
Console.WriteLine($"Difference between {setKey1} and {setKey}: {string.Join(", ", diffMembers)}");

//SETINTER : Get the intersection of two sets
var interMembers = await db.SetCombineAsync(SetOperation.Intersect, setKey1, setKey);
Console.WriteLine($"Intersection between {setKey1} and {setKey}: {string.Join(", ", interMembers)}");

//SETDIFFSTORE : Store the difference between two sets into a new set
string diffSetKey = "set_difference";
await db.SetCombineAndStoreAsync(SetOperation.Difference, diffSetKey, setKey1, setKey);
var diffSetMembers = await db.SetMembersAsync(diffSetKey);
Console.WriteLine($"Members of {diffSetKey}: {string.Join(", ", diffSetMembers)}");

//smove: Move member from one set to another
bool smoveResult = await db.SetMoveAsync(setKey1, setKey, "ip_4");
Console.WriteLine($"Moved ip_2 from {setKey1} to {setKey}: {smoveResult}");

//SPOP: Remove and return a random member from the set
var poppedMember = await db.SetPopAsync(setKey1);
Console.WriteLine($"Popped member from {setKey1}: {poppedMember}");

//srANDMEMBER: Get a random member from the set without removing it
var randomMember = await db.SetRandomMemberAsync(setKey1);
Console.WriteLine($"Random member from {setKey1}: {randomMember}");

//SREM: Remove a member from the set
bool sremResult = await db.SetRemoveAsync(setKey1, "ip_1");
Console.WriteLine($"Removed ip_1 from {setKey1}: {sremResult}");

//SUNION: Get the union of two sets
var unionMembers = await db.SetCombineAsync(SetOperation.Union, setKey1, setKey);
Console.WriteLine($"Union of {setKey1} and {setKey}: {string.Join(", ", unionMembers)}");

//SUNIONSTORE: Store the union of two sets into a new set
string unionSetKey = "set_union";
await db.SetCombineAndStoreAsync(SetOperation.Union, unionSetKey, setKey1, setKey);
    var unionSetMembers = await db.SetMembersAsync(unionSetKey);
Console.WriteLine($"Members of {unionSetKey}: {string.Join(", ", unionSetMembers)}");

//sCAN: Iterate over set members
Console.WriteLine($"Scanning members of {setKey1}:");
foreach (var member in db.SetScan(setKey1, "ip_*", pageSize: 2))
{
    Console.WriteLine($"  Found member: {member}");
}

// 5. Sorted Set 操作 (有序集合)
//Redis 有序集合和集合一样也是 string 类型元素的集合,且不允许重复的成员。
//不同的是每个元素都会关联一个 double 类型的分数。redis 正是通过分数来为集合中的成员进行从小到大的排序。
//有序集合的成员是唯一的, 但分数(score)却可以重复。
//集合是通过哈希表实现的，所以添加，删除，查找的复杂度都是 O(1)。 集合中最大的成员数为 232 - 1 (4294967295, 每个集合可存储40多亿个成员)。
Console.WriteLine("\n--- Sorted Set Operations ---");
string leaderboardKey = "game_leaderboard";

await db.SortedSetAddAsync(leaderboardKey, "PlayerA", 100);
await db.SortedSetAddAsync(leaderboardKey, "PlayerB", 200);
await db.SortedSetAddAsync(leaderboardKey, "PlayerC", 50);

//ZRANGE: Get all players in ascending order
var allPlayers = await db.SortedSetRangeByRankWithScoresAsync(leaderboardKey, 0, -1, Order.Ascending);
Console.WriteLine("All Players in Ascending Order:");
foreach (var player in allPlayers)
{
    Console.WriteLine($"  {player.Element}: {player.Score}");
}

// 获取前三名 (按分数从高到低)
var topPlayers = await db.SortedSetRangeByRankWithScoresAsync(leaderboardKey, 0, 2, Order.Descending);
Console.WriteLine("Top Players:");
foreach (var player in topPlayers)
{
    Console.WriteLine($"  {player.Element}: {player.Score}");
}

// 获取 PlayerB 的排名 (按分数从高到低)
long? rank = await db.SortedSetRankAsync(leaderboardKey, "PlayerB", Order.Descending);
Console.WriteLine($"PlayerB's Rank: {rank + 1}");
// 获取 PlayerB 的分数
double? score = await db.SortedSetScoreAsync(leaderboardKey, "PlayerB");
Console.WriteLine($"PlayerB's Score: {score}");

//ZINCRBY: Increment PlayerA's score by 50
double newScore = await db.SortedSetIncrementAsync(leaderboardKey, "PlayerA", 50);
Console.WriteLine($"PlayerA's new Score: {newScore}");

//ZREM: Remove PlayerC from the leaderboard
bool removedFromLeaderboard = await db.SortedSetRemoveAsync(leaderboardKey, "PlayerC");
Console.WriteLine($"Removed PlayerC from leaderboard: {removedFromLeaderboard}");

//ZCARD: Get the number of players in the leaderboard
long playerCount = await db.SortedSetLengthAsync(leaderboardKey);
Console.WriteLine($"Number of players in leaderboard: {playerCount}");

//ZRANGEBYSCORE: Get players with scores between 100 and 200
var midRangePlayers = await db.SortedSetRangeByScoreWithScoresAsync(leaderboardKey, 100, 200);
Console.WriteLine("Players with scores between 100 and 200:");
foreach (var player in midRangePlayers)
{
    Console.WriteLine($"  {player.Element}: {player.Score}");
}

//ZREMRANGEBYRANK: Remove players ranked 3rd and below
long removedCountZRemRange = await db.SortedSetRemoveRangeByRankAsync(leaderboardKey, 2, -1);
Console.WriteLine($"Removed {removedCountZRemRange} players ranked 3rd and below.");

// ZREMRANGEBYSCORE: Remove players with scores below 100
long removedCountByScore = await db.SortedSetRemoveRangeByScoreAsync(leaderboardKey, double.NegativeInfinity, 99);
Console.WriteLine($"Removed {removedCountByScore} players with scores below 100.");

// ZSCAN: Iterate over sorted set members
Console.WriteLine($"Scanning members of {leaderboardKey}:");
foreach (var entry in db.SortedSetScan(leaderboardKey, "*", pageSize: 2))
{
    Console.WriteLine($"  Found: {entry.Element} -> {entry.Score}");
}

//ZCOUNT: Count members with scores between a range
long countInRange = await db.SortedSetLengthAsync(leaderboardKey, 100, 200);
Console.WriteLine($"Number of players with scores between 100 and 200: {countInRange}");

//zinterstore: Intersect two sorted sets and store the result
//计算给定的一个或多个有序集的交集并将结果集存储在新的有序集合 destination 中
string anotherLeaderboardKey = "another";
await db.SortedSetAddAsync(anotherLeaderboardKey, "PlayerA", 150);
await db.SortedSetAddAsync(anotherLeaderboardKey, "PlayerD", 300);
string intersectedKey = "intersected_leaderboard";
await db.SortedSetCombineAndStoreAsync(SetOperation.Intersect, intersectedKey, new RedisKey[] { leaderboardKey, anotherLeaderboardKey });
var intersectedMembers = await db.SortedSetRangeByRankWithScoresAsync(intersectedKey, 0, -1);
Console.WriteLine("Members in intersected leaderboard:");
foreach (var member in intersectedMembers)
{
    Console.WriteLine($"  {member.Element}: {member.Score}");
}

//ZLEXCOUNT: Count members in a lexicographical range
long lexCount = await db.SortedSetLengthByValueAsync(leaderboardKey, "PlayerA", "PlayerZ");
Console.WriteLine($"Number of players between 'PlayerA' and 'PlayerZ': {lexCount}");

// ZRANGEBYLEX: Get members in a lexicographical range
var lexMembers = await db.SortedSetRangeByValueAsync(leaderboardKey, "PlayerA", "PlayerZ");
Console.WriteLine("Players between 'PlayerA' and 'PlayerZ':");
foreach (var member in lexMembers)
{
    Console.WriteLine($"  {member}");
}

//ZREMRANGEBYLEX: Remove members in a lexicographical range
long removedLexCount = await db.SortedSetRemoveRangeByValueAsync(leaderboardKey, "PlayerA", "PlayerB");
Console.WriteLine($"Removed {removedLexCount} players between 'PlayerA' and 'PlayerB'.");

//ZRANK: Get the rank of a member
long? rankPlayerA = await db.SortedSetRankAsync(leaderboardKey, "PlayerA");
Console.WriteLine($"PlayerA's Rank: {rankPlayerA + 1}");

// 清理连接
redis.Close();
