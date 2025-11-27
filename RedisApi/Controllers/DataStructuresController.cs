using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace RedisApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataStructuresController : ControllerBase
    {
        private readonly IDatabase _db;

        public DataStructuresController(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        // ==================================================================================
        // 1. String: 计数器 (文章阅读量)
        // 场景：高并发计数，如视频播放量、文章阅读数。
        // 优势：原子递增，高性能。
        // ==================================================================================
        [HttpPost("string/article-view/{articleId}")]
        public async Task<IResult> IncrementArticleView(int articleId)
        {
            string key = $"article:{articleId}:views";
            // INCR 是原子操作，线程安全
            long newCount = await _db.StringIncrementAsync(key);
            return Results.Ok(new { ArticleId = articleId, Views = newCount });
        }

        [HttpGet("string/article-view/{articleId}")]
        public async Task<IResult> GetArticleView(int articleId)
        {
            string key = $"article:{articleId}:views";
            var count = await _db.StringGetAsync(key);
            return Results.Ok(new { ArticleId = articleId, Views = (long)count });
        }

        // ==================================================================================
        // 2. Hash: 购物车 (存储对象)
        // 场景：存储对象属性，如购物车、用户资料。
        // 优势：可以单独读写字段，比存储整个 JSON 字符串更灵活、节省网络流量。
        // ==================================================================================
        [HttpPost("hash/cart/{userId}")]
        public async Task<IResult> AddToCart(int userId, int productId, int quantity)
        {
            string key = $"cart:{userId}";
            // HINCRBY: 如果字段不存在则创建，存在则累加
            await _db.HashIncrementAsync(key, productId, quantity);
            return Results.Ok($"Added product {productId} to user {userId}'s cart.");
        }

        [HttpGet("hash/cart/{userId}")]
        public async Task<IResult> GetCart(int userId)
        {
            string key = $"cart:{userId}";
            // HGETALL: 获取所有字段和值
            HashEntry[] cart = await _db.HashGetAllAsync(key);
            
            // 转换为字典返回
            var result = cart.ToDictionary(x => x.Name.ToString(), x => (int)x.Value);
            return Results.Ok(result);
        }

        // ==================================================================================
        // 3. List: 消息队列 (简单的任务队列)
        // 场景：简单的生产者-消费者模型，最新消息流(Timeline)。
        // 优势：有序，支持阻塞读取 (BLPOP/BRPOP)。
        // ==================================================================================
        [HttpPost("list/queue")]
        public async Task<IResult> EnqueueTask(string taskName)
        {
            string key = "task_queue";
            // LPUSH: 从左侧推入
            await _db.ListLeftPushAsync(key, taskName);
            return Results.Ok($"Task '{taskName}' enqueued.");
        }

        [HttpGet("list/queue/process")]
        public async Task<IResult> ProcessTask()
        {
            string key = "task_queue";
            // RPOP: 从右侧弹出
            var task = await _db.ListRightPopAsync(key);
            if (task.HasValue)
            {
                return Results.Ok($"Processing task: {task}");
            }
            return Results.Ok("No tasks in queue.");
        }

        // ==================================================================================
        // 4. Set: 共同好友/共同兴趣 (交集)
        // 场景：标签系统、抽奖(去重)、社交关系(共同关注)。
        // 优势：自动去重，支持集合运算(交集、并集、差集)。
        // ==================================================================================
        [HttpPost("set/interests")]
        public async Task<IResult> AddInterests(int userId, string[] interests)
        {
            string key = $"user:{userId}:interests";
            var values = interests.Select(i => (RedisValue)i).ToArray();
            // SADD: 添加元素，自动去重
            await _db.SetAddAsync(key, values);
            return Results.Ok($"Interests added for user {userId}");
        }

        [HttpGet("set/common-interests")]
        public async Task<IResult> GetCommonInterests(int user1Id, int user2Id)
        {
            string key1 = $"user:{user1Id}:interests";
            string key2 = $"user:{user2Id}:interests";
            // SINTER: 计算交集
            RedisValue[] common = await _db.SetCombineAsync(SetOperation.Intersect, key1, key2);
            return Results.Ok(common.Select(x => x.ToString()));
        }

        // ==================================================================================
        // 5. Sorted Set (ZSet): 排行榜
        // 场景：游戏排行榜、热搜榜、带权重的队列。
        // 优势：元素唯一且有序，支持按分数范围查询。
        // ==================================================================================
        [HttpPost("zset/score")]
        public async Task<IResult> UpdateScore(string player, double score)
        {
            string key = "game:leaderboard";
            // ZADD: 添加或更新分数
            await _db.SortedSetAddAsync(key, player, score);
            return Results.Ok($"Updated score for {player}");
        }

        [HttpGet("zset/leaderboard")]
        public async Task<IResult> GetLeaderboard()
        {
            string key = "game:leaderboard";
            // ZREVRANGE 0 9 WITHSCORES: 获取前10名 (分数从高到低)
            var topPlayers = await _db.SortedSetRangeByRankWithScoresAsync(key, stop: 9, order: Order.Descending);
            
            var result = topPlayers.Select(x => new { Player = x.Element.ToString(), Score = x.Score });
            return Results.Ok(result);
        }

        // ==================================================================================
        // 6. Bitmap: 用户签到
        // 场景：用户签到、在线状态、活跃用户统计。
        // 优势：极度节省空间 (1 bit 代表一个状态)。
        // ==================================================================================
        [HttpPost("bitmap/signin")]
        public async Task<IResult> SignIn(int userId, DateTime date)
        {
            // Key 格式: signin:{userId}:{yyyyMM}
            string key = $"signin:{userId}:{date:yyyyMM}";
            // 将日期作为偏移量 (0-30)
            long offset = date.Day - 1;
            
            // SETBIT: 设置某一位为 1
            await _db.StringSetBitAsync(key, offset, true);
            return Results.Ok($"User {userId} signed in on {date:yyyy-MM-dd}");
        }

        [HttpGet("bitmap/signin/count")]
        public async Task<IResult> GetSignInCount(int userId, string month) // month: yyyyMM
        {
            string key = $"signin:{userId}:{month}";
            // BITCOUNT: 统计 1 的个数
            long count = await _db.StringBitCountAsync(key);
            return Results.Ok(new { UserId = userId, Month = month, DaysSignedIn = count });
        }

        // ==================================================================================
        // 7. Geo: 附近的人/店铺
        // 场景：LBS 应用、附近商家、距离计算。
        // 优势：基于 GeoHash，支持半径查询。
        // ==================================================================================
        [HttpPost("geo/shop")]
        public async Task<IResult> AddShop(string name, double lon, double lat)
        {
            string key = "shops:locations";
            // GEOADD: 添加地理位置
            await _db.GeoAddAsync(key, lon, lat, name);
            return Results.Ok($"Added shop {name}");
        }

        [HttpGet("geo/nearby")]
        public async Task<IResult> FindNearby(double lon, double lat, double radiusKm)
        {
            string key = "shops:locations";
            // GEOSEARCH / GEORADIUS: 搜索附近的点
            var results = await _db.GeoRadiusAsync(key, lon, lat, radiusKm, GeoUnit.Kilometers);
            
            var shops = results.Select(x => new 
            { 
                Name = x.Member.ToString(),
                Distance = x.Distance.HasValue ? $"{x.Distance.Value:F2} km" : "Unknown",
                Position = x.Position.HasValue ? new { Lon = x.Position.Value.Longitude, Lat = x.Position.Value.Latitude } : null
            });
            
            return Results.Ok(shops);
        }
        
        // ==================================================================================
        // 8. HyperLogLog: 网站 UV (基数统计)
        // 场景：统计海量数据的唯一值 (如 UV)，允许少量误差 (0.81%)。
        // 优势：占用空间极小且固定 (约 12KB)，无论统计多少数据。
        // ==================================================================================
        [HttpPost("hll/visit")]
        public async Task<IResult> RecordVisit(string page, string ip)
        {
            string key = $"uv:{page}:{DateTime.Now:yyyyMMdd}";
            // PFADD: 添加元素
            await _db.HyperLogLogAddAsync(key, ip);
            return Results.Ok("Visit recorded");
        }

        [HttpGet("hll/count")]
        public async Task<IResult> GetUv(string page)
        {
            string key = $"uv:{page}:{DateTime.Now:yyyyMMdd}";
            // PFCOUNT: 估算基数
            long count = await _db.HyperLogLogLengthAsync(key);
            return Results.Ok(new { Page = page, UV = count });
        }
    }
}