using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace JwtTokenMiddleware.Redis
{
    public static class RedisConnectionExtension
    {
        public static IServiceCollection AddRedisServer(this IServiceCollection services, string RedisConnectionString)
        {
           
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(RedisConnectionString));
            return services;
        }
    }
}
