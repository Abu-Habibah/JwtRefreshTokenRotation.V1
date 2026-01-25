using System;
using System.Threading.Tasks;
using Xunit;
using StackExchange.Redis;
using TokenMiddleware.RateLimiter;

namespace JwtTokenMiddleware.Session.Test
{
    public class SessionServiceTests : IAsyncLifetime
    {
        private IConnectionMultiplexer _mux = null!;
        private ISessionService _sessions = null!;

        public async Task InitializeAsync()
        {
            // Connect to a local Redis instance (Docker: localhost:6379)
            _mux = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            _sessions = new RedisSessionService(_mux);
        }

        public Task DisposeAsync()
        {
            _mux.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Create_And_Validate_Session_Works()
        {
            var payload = new SessionPayload("user123", "Admin", "FullAccess");
            var token = await _sessions.CreateSessionAsync(payload, TimeSpan.FromSeconds(10));

            var validated = await _sessions.ValidateSessionAsync(token);
            Assert.NotNull(validated);
            Assert.Equal("user123", validated!.UserId);
            Assert.Equal("Admin", validated.Role);
        }

        [Fact]
        public async Task SlidingExpiration_Extends_TTL()
        {
            var payload = new SessionPayload("user456", "User", "Limited");
            var token = await _sessions.CreateSessionAsync(payload, TimeSpan.FromSeconds(5));

            // Validate with sliding TTL extension
            var validated = await _sessions.ValidateSessionAsync(token, TimeSpan.FromSeconds(30));
            Assert.NotNull(validated);

            // Check TTL extended
            var ttl = await _mux.GetDatabase().KeyTimeToLiveAsync($"session:{token}");
            Assert.True(ttl!.Value.TotalSeconds > 10);
        }

        [Fact]
        public async Task Invalidate_Removes_Session()
        {
            var payload = new SessionPayload("user789", "Guest", "ReadOnly");
            var token = await _sessions.CreateSessionAsync(payload, TimeSpan.FromSeconds(30));

            await _sessions.InvalidateSessionAsync(token);

            var validated = await _sessions.ValidateSessionAsync(token);
            Assert.Null(validated);
        }
    }
}
