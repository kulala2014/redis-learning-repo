using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace RedisApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;

    public AdminController(IDistributedCache cache, IConnectionMultiplexer redis)
    {
        _cache = cache;
        _redis = redis;
    }

    // 演示 4: 模拟缓存预热 (防止雪崩)
    [HttpPost("preheat")]
    public async Task<IActionResult> Preheat()
    {
        var results = new List<string>();
        var baseExpiry = TimeSpan.FromMinutes(30);
        
        // 模拟批量预热 10 个热门商品
        for (int i = 1; i <= 10; i++)
        {
            string key = $"product:{i}";
            string value = $"Preheated data for {i}";
            
            // 关键点：引入随机抖动 (Jitter)
            var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 300)); // 0-5分钟随机
            var finalExpiry = baseExpiry + jitter;

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = finalExpiry
            };

            await _cache.SetStringAsync(key, value, options);
            results.Add($"Key: {key}, Expiry: {finalExpiry.TotalSeconds:F0}s (Base: {baseExpiry.TotalSeconds}s + Jitter: {jitter.TotalSeconds}s)");
        }

        return Ok(results);
    }

    // 演示 5: Big Key 处理 (UNLINK vs DEL)
    [HttpPost("bigkey-test")]
    public async Task<IActionResult> BigKeyTest()
    {
        var db = _redis.GetDatabase();
        string bigKey = "big_list_key";

        // 1. 制造一个 Big Key (如果不存在)
        if (!await db.KeyExistsAsync(bigKey))
        {
            Console.WriteLine("Generating Big Key...");
            // 使用 Pipeline 快速写入 10万条数据 (模拟 Big Key)
            var batch = db.CreateBatch();
            var tasks = new List<Task>();
            for (int i = 0; i < 100000; i++)
            {
                tasks.Add(batch.ListRightPushAsync(bigKey, $"item_{i}"));
            }
            batch.Execute();
            await Task.WhenAll(tasks);
            Console.WriteLine("Big Key Generated.");
        }

        // 2. 比较 DEL 和 UNLINK
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // StackExchange.Redis 的 KeyDeleteAsync 默认是 DEL。
        // 这里演示显式调用 UNLINK (通过 Execute)
        await db.ExecuteAsync("UNLINK", bigKey);
        
        sw.Stop();
        return Ok($"Big Key unlinked in {sw.ElapsedMilliseconds}ms (Async)");
    }

    // 演示 7: Pipeline (批量操作)
    [HttpPost("pipeline-test")]
    public async Task<IActionResult> PipelineTest()
    {
        var db = _redis.GetDatabase();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Pipeline (快)
        var batch = db.CreateBatch();
        var tasks = new List<Task>();
        
        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(batch.StringSetAsync($"batch:{i}", i));
        }

        // 一次性发送所有命令
        batch.Execute();
        
        // 等待所有结果返回
        await Task.WhenAll(tasks);

        sw.Stop();
        return Ok($"Pipeline executed 1000 commands in {sw.ElapsedMilliseconds}ms");
    }
}
