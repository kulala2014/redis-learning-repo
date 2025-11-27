using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using RedisApi.Data;
using RedisApi.Models;
using RedLockNet;
using StackExchange.Redis;
using System.Text.Json;

namespace RedisApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _localCache;
    private readonly IDistributedLockFactory _lockFactory;
    private readonly IConnectionMultiplexer _redis;

    // 定义 L1 缓存中的空值标记 (用于防止穿透)
    private static readonly object L1_NULL_MARKER = new object();
    private const string CACHE_NULL_VALUE = "@@NULL@@"; // L2 Redis 空值标记

    public ProductsController(
        AppDbContext dbContext,
        IDistributedCache cache,
        IMemoryCache localCache,
        IDistributedLockFactory lockFactory,
        IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _cache = cache;
        _localCache = localCache;
        _lockFactory = lockFactory;
        _redis = redis;
    }

    // 演示 3: 多级缓存 (L1 Memory + L2 Redis) + 分布式锁 + 防止缓存穿透
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        string cacheKey = $"product:{id}";

        // ==================================================================================
        // 1. L1 Cache Check (Memory) - 速度最快，无网络开销
        // ==================================================================================
        if (_localCache.TryGetValue(cacheKey, out object? l1Value))
        {
            if (l1Value == L1_NULL_MARKER)
            {
                Console.WriteLine($"Product {id} found in L1 Cache (NULL Marker)");
                return NotFound();
            }
            if (l1Value is Product p)
            {
                // Console.WriteLine($"Product {id} found in L1 Cache (Hit)");
                return Ok(p);
            }
        }

        // ==================================================================================
        // 2. L2 Cache Check (Redis) - 速度快，有网络开销
        // ==================================================================================
        string? l2Value = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(l2Value))
        {
            // 2.1 处理 Redis 中的空值标记
            if (l2Value == CACHE_NULL_VALUE)
            {
                Console.WriteLine($"Product {id} found in L2 Cache (NULL Marker)");
                // 回填 L1 (短 TTL)
                _localCache.Set(cacheKey, L1_NULL_MARKER, TimeSpan.FromSeconds(10));
                return NotFound();
            }

            // 2.2 处理 Redis 中的有效数据
            var product = JsonSerializer.Deserialize<Product>(l2Value);
            if (product != null)
            {
                Console.WriteLine($"Product {id} found in L2 Cache (Hit)");
                // 回填 L1 (短 TTL，例如 30秒，减少 L1 数据不一致的时间窗口)
                _localCache.Set(cacheKey, product, TimeSpan.FromSeconds(30));
                return Ok(product);
            }
        }

        // ==================================================================================
        // 3. Cache Miss - 准备查库 (加分布式锁)
        // ==================================================================================
        string lockKey = $"lock:product:{id}";
        var expiry = TimeSpan.FromSeconds(30);
        var wait = TimeSpan.FromSeconds(10);
        var retry = TimeSpan.FromSeconds(1);

        using (var redLock = await _lockFactory.CreateLockAsync(lockKey, expiry, wait, retry))
        {
            if (redLock.IsAcquired)
            {
                // 3.1 Double Check L2 (Redis)
                l2Value = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(l2Value))
                {
                    if (l2Value == CACHE_NULL_VALUE)
                    {
                        _localCache.Set(cacheKey, L1_NULL_MARKER, TimeSpan.FromSeconds(10));
                        return NotFound();
                    }
                    var cachedProduct = JsonSerializer.Deserialize<Product>(l2Value);
                    if (cachedProduct != null)
                    {
                        _localCache.Set(cacheKey, cachedProduct, TimeSpan.FromSeconds(30));
                        return Ok(cachedProduct);
                    }
                }

                // 3.2 Query Database
                Console.WriteLine($"Product {id} not found, fetching from DB (Miss)");
                var product = await _dbContext.Products.FindAsync(id);

                if (product == null)
                {
                    Console.WriteLine($"Product {id} does not exist in DB. Caching NULL marker.");
                    // 写入 L2 (Redis)
                    await _cache.SetStringAsync(cacheKey, CACHE_NULL_VALUE, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
                    // 写入 L1 (Memory)
                    _localCache.Set(cacheKey, L1_NULL_MARKER, TimeSpan.FromSeconds(10));
                    
                    return NotFound();
                }

                // 3.3 Write Back
                // 写入 L2 (Redis) - TTL 5分钟
                // 防止缓存雪崩 (Cache Avalanche):
                var baseExpiry = TimeSpan.FromMinutes(5);
                var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 60)); // 0-60秒随机抖动
                
                await _cache.SetStringAsync(
                    cacheKey, 
                    JsonSerializer.Serialize(product), 
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = baseExpiry + jitter }
                );
                
                // 写入 L1 (Memory) - TTL 30秒
                _localCache.Set(cacheKey, product, TimeSpan.FromSeconds(30));

                return Ok(product);
            }
            else
            {
                return StatusCode(429); // Too Many Requests
            }
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Product product)
    {
        // 1. 写入数据库 (获取生成的 ID)
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        string cacheKey = $"product:{product.Id}";
        string lockKey = $"lock:product:{product.Id}";

        // 2. 获取分布式锁
        using (var redLock = await _lockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)))
        {
            if (redLock.IsAcquired)
            {
                // 3. 写入/更新 L2 缓存 (Redis)
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), cacheOptions);

                // 4. 移除 L1 缓存 (Memory)
                _localCache.Remove(cacheKey);
            }
            else
            {
                // 极端情况：锁获取失败。
                await _cache.RemoveAsync(cacheKey);
                _localCache.Remove(cacheKey);
            }
        }

        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Product inputProduct)
    {
        // 1. 查找数据库中的现有记录
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return NotFound();

        // 2. 更新数据库
        product.Name = inputProduct.Name;
        product.Price = inputProduct.Price;
        product.Description = inputProduct.Description;
        await _dbContext.SaveChangesAsync();

        string cacheKey = $"product:{id}";
        string lockKey = $"lock:product:{id}";

        // 3. 获取分布式锁
        using (var redLock = await _lockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)))
        {
            if (redLock.IsAcquired)
            {
                // 4. 删除 L2 缓存 (Redis)
                await _cache.RemoveAsync(cacheKey);

                // 5. 删除 L1 缓存 (Memory)
                _localCache.Remove(cacheKey);
                
                Console.WriteLine($"Product {id} updated. Cache invalidated.");
            }
            else
            {
                // 锁获取失败，为了安全起见，尝试强制删除缓存
                await _cache.RemoveAsync(cacheKey);
                _localCache.Remove(cacheKey);
            }
        }

        return NoContent();
    }

    // 演示 6: Lua 脚本 (原子性扣减库存)
    [HttpPost("seckill/{productId}")]
    public async Task<IActionResult> Seckill(int productId)
    {
        var db = _redis.GetDatabase();
        string stockKey = $"stock:{productId}";

        // 初始化库存 (仅测试用)
        await db.StringSetAsync(stockKey, 10, when: When.NotExists);

        string script = @"
            if redis.call('exists', KEYS[1]) == 1 then
                local stock = tonumber(redis.call('get', KEYS[1]))
                if stock > 0 then
                    redis.call('decr', KEYS[1])
                    return 1
                end
            end
            return 0
        ";

        var result = (int)await db.ScriptEvaluateAsync(script, new RedisKey[] { stockKey });

        if (result == 1)
        {
            Console.WriteLine($"Seckill success! Product: {productId}");
            return Ok("Seckill Success!");
        }
        else
        {
            Console.WriteLine($"Seckill failed! Product: {productId}");
            return BadRequest("Seckill Failed: Out of stock.");
        }
    }
}
