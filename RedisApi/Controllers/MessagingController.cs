using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace RedisApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagingController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;

    public MessagingController(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    // ==================================================================================
    // 1. Pub/Sub (发布/订阅)
    // 场景：实时通知、配置刷新。
    // 特点：消息不持久化，消费者不在线就收不到。
    // ==================================================================================
    [HttpPost("pubsub/publish")]
    public async Task<IResult> PublishMessage(string message)
    {
        var sub = _redis.GetSubscriber();
        // 发布消息到 "alerts" 频道
        long receivers = await sub.PublishAsync(RedisChannel.Literal("alerts"), message);
        return Results.Ok($"Message published to {receivers} subscribers.");
    }

    // ==================================================================================
    // 2. Streams (流数据)
    // 场景：持久化消息队列、事件溯源。
    // 特点：消息持久化，支持消费者组。
    // ==================================================================================
    [HttpPost("stream/add")]
    public async Task<IResult> AddToStream(string user, string action)
    {
        var db = _redis.GetDatabase();
        string streamKey = "events:stream";

        // XADD: 添加消息
        // 自动生成 ID (*)
        // 存储键值对: "user", "action", "time"
        var id = await db.StreamAddAsync(streamKey, new NameValueEntry[]
        {
            new NameValueEntry("user", user),
            new NameValueEntry("action", action),
            new NameValueEntry("time", DateTime.Now.ToString("O"))
        });

        return Results.Ok($"Event added to stream with ID: {id}");
    }

    [HttpGet("stream/read")]
    public async Task<IResult> ReadFromStream(int count = 5)
    {
        var db = _redis.GetDatabase();
        string streamKey = "events:stream";

        // XRANGE: 读取消息 (从开始到结束)
        // "-" 表示最小 ID, "+" 表示最大 ID
        var events = await db.StreamRangeAsync(streamKey, "-", "+", count);

        var result = events.Select(e => new
        {
            Id = e.Id.ToString(),
            Values = e.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString())
        });

        return Results.Ok(result);
    }
    
    // ==================================================================================
    // 3. Keyspace Notifications (键空间通知)
    // 场景：订单超时自动取消。
    // 原理：监听 Key 的过期事件 (__keyevent@0__:expired)。
    // 注意：需要 Redis 开启 notify-keyspace-events Ex 配置。
    // ==================================================================================
    [HttpPost("keyspace/create-order")]
    public async Task<IResult> CreateOrder(int orderId)
    {
        var db = _redis.GetDatabase();
        // 模拟创建一个订单，5秒后过期 (模拟超时未支付)
        // 实际业务中，会有一个 BackgroundService 监听过期事件，收到通知后修改数据库订单状态。
        await db.StringSetAsync($"order:{orderId}", "pending", TimeSpan.FromSeconds(5));
        
        return Results.Ok($"Order {orderId} created. Will expire in 5 seconds. (Check logs if listener is active)");
    }
}
