using StackExchange.Redis;
using System.Text.Json;

namespace TokenMiddleware.SessionToken
{
    public class RedisSessionService : ISessionService
    {
        private readonly IDatabase _db;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public RedisSessionService(IConnectionMultiplexer mux)
        {
            _db = mux.GetDatabase();
        }

        public async Task<string> CreateSessionAsync(SessionPayload payload, TimeSpan ttl)
        {
            var token = Guid.NewGuid().ToString("N");
            var key = $"{GeneralConst.SESSION_TOKEN_MARKER}:{token}";
            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            await _db.StringSetAsync(key, json, ttl);
            return token;
        }

        public async Task<SessionPayload?> ValidateSessionAsync(string token, TimeSpan? slidingTtl = null)
        {
            var key = $"{GeneralConst.SESSION_TOKEN_MARKER}:{token}";
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;

            if (slidingTtl.HasValue)
                await _db.KeyExpireAsync(key, slidingTtl);

            return JsonSerializer.Deserialize<SessionPayload>(value.ToString(), _jsonOptions);
        }

        public Task InvalidateSessionAsync(string token)
            => _db.KeyDeleteAsync($"{GeneralConst.SESSION_TOKEN_MARKER}:{token}");
    }
}