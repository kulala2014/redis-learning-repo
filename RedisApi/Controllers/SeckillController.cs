using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace RedisApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeckillController : ControllerBase
{
    private readonly IDatabase _db;

    public SeckillController(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    // ==================================================================================
    // 初始化测试数据
    // ==================================================================================
    [HttpPost("init")]
    public async Task<IResult> InitData()
    {
        // 初始化转账测试数据
        await _db.StringSetAsync("balance:user1", 100);
        await _db.StringSetAsync("balance:user2", 0);
        
        // 初始化秒杀库存
        await _db.StringSetAsync("product:stock", 50); 
        
        return Results.Ok(new { 
            Message = "Data initialized", 
            User1Balance = 100, 
            User2Balance = 0, 
            Stock = 50 
        });
    }

    // ==================================================================================
    // 1. Redis 事务基础示例：转账
    // 场景：User1 转账 10 元给 User2
    // 原理：使用 MULTI/EXEC 保证命令批量执行。
    // 注意：Redis 事务不支持回滚（Rollback）。如果中间某条命令报错，后续命令仍会执行。
    //       但在 StackExchange.Redis 中，我们主要使用 Condition (乐观锁) 来控制并发。
    // ==================================================================================
    [HttpPost("transaction/transfer")]
    public async Task<IResult> TransferMoney()
    {
        // 创建事务
        var trans = _db.CreateTransaction();
        
        // 添加乐观锁条件 (Condition)
        // 相当于 Redis 的 WATCH 命令。
        // 这里我们简单检查：只有当 balance:user1 存在时才执行。
        // 在实际业务中，你可能需要更复杂的条件，比如 "balance:user1" 的值必须等于某个版本号。
        trans.AddCondition(Condition.KeyExists("balance:user1"));
        
        // 将命令加入事务队列 (此时不会立即执行，只返回 Task)
        // 注意：在 ExecuteAsync 之前，不要 await 这些 Task，因为它们还没执行。
        var decrTask = trans.StringDecrementAsync("balance:user1", 10);
        var incrTask = trans.StringIncrementAsync("balance:user2", 10);

        // 执行事务 (EXEC)
        // 如果 Condition 不满足，或者期间 Key 被其他客户端修改（如果使用了相关 Condition），committed 会返回 false
        bool committed = await trans.ExecuteAsync();

        if (committed)
        {
            // 事务成功，可以获取结果
            long user1Balance = await decrTask;
            long user2Balance = await incrTask;
            return Results.Ok(new { Success = true, User1 = user1Balance, User2 = user2Balance });
        }
        else
        {
            // 事务失败 (通常是因为 Condition 不满足)
            return Results.BadRequest("Transfer failed: Transaction aborted due to condition failure.");
        }
    }

    // ==================================================================================
    // 2. 秒杀场景 A: 使用 Redis 事务 (乐观锁 / CAS)
    // 原理：Watch 库存 -> 检查库存 > 0 -> Multi -> Decr -> Exec
    // 优点：利用 Redis 原生功能，不需要 Lua。
    // 缺点：在高并发下，冲突率极高，大量请求会因为 CAS 失败而重试，导致 CPU 浪费和吞吐量下降。
    // ==================================================================================
    [HttpPost("buy/transaction")]
    public async Task<IResult> BuyWithTransaction()
    {
        string stockKey = "product:stock";
        
        // 1. 读取当前库存 (作为 CAS 的期望值)
        RedisValue currentStockVal = await _db.StringGetAsync(stockKey);
        
        if (currentStockVal.IsNullOrEmpty || (int)currentStockVal <= 0)
        {
            return Results.BadRequest("Out of stock!");
        }

        // 2. 开启事务
        var trans = _db.CreateTransaction();
        
        // 3. 添加 CAS 条件：只有当 Redis 中的值 仍然等于 我们刚才读到的值 时，才执行
        // 这相当于 WATCH stockKey
        trans.AddCondition(Condition.StringEqual(stockKey, currentStockVal));
        
        // 4. 扣减库存
        var decrTask = trans.StringDecrementAsync(stockKey);

        // 5. 提交事务
        bool committed = await trans.ExecuteAsync();

        if (committed)
        {
            long remaining = await decrTask;
            return Results.Ok($"Buy success (Transaction)! Remaining: {remaining}");
        }
        else
        {
            // 提交失败，说明在读取和提交的间隙，库存被其他人修改了
            // 在秒杀场景下，这里通常需要返回 "系统繁忙，请重试" 或者由客户端自动重试
            return Results.StatusCode(409); // Conflict
        }
    }

    // ==================================================================================
    // 3. 秒杀场景 B: 使用 Lua 脚本 (推荐方案)
    // 原理：将 "检查" 和 "扣减" 逻辑打包成一个 Lua 脚本发送给 Redis。
    // 优点：Redis 保证脚本执行的原子性，中间不会插入其他命令。没有 CAS 冲突重试的问题。
    // 缺点：脚本逻辑不能太复杂，否则会阻塞 Redis 主线程。
    // ==================================================================================
    [HttpPost("buy/lua")]
    public async Task<IResult> BuyWithLua()
    {
        string stockKey = "product:stock";
        
        // Lua 脚本逻辑：
        // 1. 获取库存
        // 2. 判断库存 > 0
        // 3. 扣减并返回剩余库存
        // 4. 否则返回 -1
        string script = @"
            local stock = tonumber(redis.call('get', KEYS[1]))
            if stock and stock > 0 then
                redis.call('decr', KEYS[1])
                return stock - 1
            else
                return -1
            end
        ";

        var result = (int)await _db.ScriptEvaluateAsync(script, new RedisKey[] { stockKey });

        if (result >= 0)
        {
            return Results.Ok($"Buy success (Lua)! Remaining: {result}");
        }
        else
        {
            return Results.BadRequest("Out of stock!");
        }
    }

    // ==================================================================================
    // 4. 秒杀场景 C: 使用 Redis 分布式锁 (RedLock)
    // 原理：先加锁 -> 查库存 -> 扣库存 -> 释放锁
    // 优点：逻辑简单，强一致性，适合业务逻辑复杂的场景。
    // 缺点：性能最差。将并行操作变成了串行操作，吞吐量受限于锁的粒度。
    // ==================================================================================
    [HttpPost("buy/lock")]
    public async Task<IResult> BuyWithLock([FromServices] RedLockNet.IDistributedLockFactory lockFactory)
    {
        string stockKey = "product:stock";
        string lockKey = "lock:product:stock";

        // 1. 尝试获取分布式锁
        // expiry: 锁的自动过期时间 (防止死锁)
        // wait: 等待获取锁的时间 (如果拿不到锁，最多等多久)
        // retry: 重试间隔
        using (var redLock = await lockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100)))
        {
            if (redLock.IsAcquired)
            {
                // 2. 获取锁成功，执行业务逻辑
                // 2.1 查库存
                var stockVal = await _db.StringGetAsync(stockKey);
                int stock = (int)stockVal;

                // 2.2 判断库存
                if (stock > 0)
                {
                    // 2.3 扣减库存
                    await _db.StringDecrementAsync(stockKey);
                    return Results.Ok($"Buy success (Lock)! Remaining: {stock - 1}");
                }
                else
                {
                    return Results.BadRequest("Out of stock!");
                }
            }
            else
            {
                // 3. 获取锁失败 (说明太拥挤了)
                return Results.StatusCode(429); // Too Many Requests
            }
        }
    }

    // ==================================================================================
    // 5. 秒杀场景 D: 使用原生 SETNX 实现简单分布式锁
    // 原理：SET key value NX PX expiry
    // 优点：不依赖第三方库，理解原理。
    // 缺点：单节点 Redis 可靠性不如 RedLock (RedLock 解决了主从切换锁丢失的问题)。
    // ==================================================================================
    [HttpPost("buy/setnx")]
    public async Task<IResult> BuyWithSetNx()
    {
        string stockKey = "product:stock";
        string lockKey = "lock:product:stock:simple";
        string lockValue = Guid.NewGuid().ToString(); // 锁的唯一标识，防止误删别人的锁

        // 1. 尝试加锁 (SETNX + Expiry 原子操作)
        // 对应 Redis 命令: SET lockKey lockValue NX PX 5000
        // When.NotExists 相当于 NX
        bool locked = await _db.StringSetAsync(lockKey, lockValue, TimeSpan.FromSeconds(5), When.NotExists);

        if (locked)
        {
            try
            {
                // 2. 业务逻辑
                var stockVal = await _db.StringGetAsync(stockKey);
                int stock = (int)stockVal;

                if (stock > 0)
                {
                    await _db.StringDecrementAsync(stockKey);
                    return Results.Ok($"Buy success (SetNX)! Remaining: {stock - 1}");
                }
                else
                {
                    return Results.BadRequest("Out of stock!");
                }
            }
            finally
            {
                // 3. 释放锁 (使用 Lua 脚本保证原子性)
                // 只有当 Value 等于我们设置的 lockValue 时才删除，防止删除别人的锁
                // (比如：我的锁过期了，别人加了新锁，我不能把别人的删了)
                string releaseScript = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('del', KEYS[1])
                    else
                        return 0
                    end";
                await _db.ScriptEvaluateAsync(releaseScript, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
            }
        }
        else
        {
            return Results.StatusCode(429); // 没抢到锁
        }
    }
}
