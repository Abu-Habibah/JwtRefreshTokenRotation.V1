using JwtTokenMiddleware.RateLimiter;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using Xunit;

namespace JwtTokenMiddleware.Limiter.Test
{
    public class RateLimitingIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider _sp = null!;
        private LimitingOptions _options = null!;
        private IConnectionMultiplexer _mux = null!;

        public async Task InitializeAsync()
        {
            // Connect to a real Redis instance (Docker: localhost:6379)
            _mux = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

            _options = new LimitingOptions
            {
                AuthorizedOptions = new LimitingPolicy
                {
                    GeneralOption = new GeneralLimitingOption
                    {
                        MaxRequests = 2,
                        WindowSpan = TimeSpan.FromSeconds(30)
                    }
                },
                UnauthorizedOptions = new LimitingPolicy
                {
                    GeneralOption = new GeneralLimitingOption
                    {
                        MaxRequests = 1,
                        WindowSpan = TimeSpan.FromSeconds(30)
                    }
                },
                FallbackMode = RateLimitFallbackMode.FailFast
            };

            var services = new ServiceCollection();
            services.AddSingleton<IConnectionMultiplexer>(_mux);
            services.AddRateLimiter(_options);

            _sp = services.BuildServiceProvider();
        }

        public Task DisposeAsync()
        {
            _sp.Dispose();
            _mux.Dispose();
            return Task.CompletedTask;
        }

        private HttpContext CreateContext(bool authenticated = false)
        {
            var ctx = new DefaultHttpContext();
            if (authenticated)
            {
                ctx.User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity("TestAuth"));
            }
            return ctx;
        }

        [Fact]
        public async Task Allows_Request_When_Under_Limit()
        {
            var loadedScript = _sp.GetRequiredService<LoadedLuaScript>();

            var middleware = new RateLimitingMiddleware(
                ctx => Task.CompletedTask,
                _mux,
                _options,
                loadedScript);

            var ctxHttp = CreateContext(authenticated: true);

            await middleware.InvokeAsync(ctxHttp);

            Assert.NotEqual(StatusCodes.Status429TooManyRequests, ctxHttp.Response.StatusCode);
        }

        [Fact]
        public async Task Blocks_Request_When_Over_Limit()
        {
            var loadedScript = _sp.GetRequiredService<LoadedLuaScript>();

            var middleware = new RateLimitingMiddleware(
                ctx => Task.CompletedTask,
                _mux,
                _options,
                loadedScript);

            var ctxHttp = CreateContext(authenticated: true);

            // Hit the middleware multiple times to exceed the limit
            await middleware.InvokeAsync(ctxHttp);
            await middleware.InvokeAsync(ctxHttp);
            await middleware.InvokeAsync(ctxHttp);

            Assert.Equal(StatusCodes.Status429TooManyRequests, ctxHttp.Response.StatusCode);
        }
    }
}
