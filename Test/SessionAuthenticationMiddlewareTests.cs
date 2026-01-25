using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using TokenMiddleware.RateLimiter;
using Xunit;

namespace JwtTokenMiddleware.Session.Test
{
    public class SessionAuthenticationMiddlewareTests : IAsyncLifetime
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

        private static HttpContext CreateContext(string? token = null)
        {
            var ctx = new DefaultHttpContext();
            if (token != null)
            {
                ctx.Request.Headers["Authorization"] = $"Session {token}";
            }
            return ctx;
        }

        [Fact]
        public async Task Middleware_Populates_User_When_Valid_Token()
        {
            // Arrange: create a session token
            var payload = new SessionPayload("user123", "Admin", "FullAccess");
            var token = await _sessions.CreateSessionAsync(payload, TimeSpan.FromMinutes(5));

            var middleware = new SessionAuthenticationMiddleware(_ => Task.CompletedTask);
            var ctx = CreateContext(token);

            // Act
            await middleware.InvokeAsync(ctx, _sessions);

            // Assert
            Assert.True(ctx.User.Identity?.IsAuthenticated);
            Assert.Equal("user123", ctx.User.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal("Admin", ctx.User.FindFirstValue(ClaimTypes.Role));
            Assert.Equal("FullAccess", ctx.User.FindFirst("policy")?.Value);
        }

        [Fact]
        public async Task Middleware_Returns_401_When_Invalid_Token()
        {
            var middleware = new SessionAuthenticationMiddleware(_ => Task.CompletedTask);
            var ctx = CreateContext("nonexistent-token");

            await middleware.InvokeAsync(ctx, _sessions);

            Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        }

        [Fact]
        public async Task Middleware_Skips_When_No_Header()
        {
            var middleware = new SessionAuthenticationMiddleware(_ => Task.CompletedTask);
            var ctx = CreateContext();

            await middleware.InvokeAsync(ctx, _sessions);

            // No Authorization header → User should remain unauthenticated
            Assert.False(ctx.User.Identity?.IsAuthenticated ?? false);
            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        }
    }
}
