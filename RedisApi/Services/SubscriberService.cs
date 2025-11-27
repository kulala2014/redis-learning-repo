using StackExchange.Redis;

namespace RedisApi.Services;

public class SubscriberService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SubscriberService> _logger;

    public SubscriberService(IConnectionMultiplexer redis, ILogger<SubscriberService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();

        // 订阅 "alerts" 频道
        // 注意：这是阻塞的吗？在 StackExchange.Redis 中，Subscribe 是非阻塞的，回调是异步执行的。
        // 但我们需要保持 Service 运行，所以 ExecuteAsync 不能直接退出。
        await sub.SubscribeAsync(RedisChannel.Literal("alerts"), (channel, message) =>
        {
            _logger.LogInformation($"[Pub/Sub] Received alert: {message}");
        });

        _logger.LogInformation("[Pub/Sub] Subscribed to 'alerts' channel.");

        // 保持服务运行
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
