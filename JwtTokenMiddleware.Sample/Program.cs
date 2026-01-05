using JwtTokenMiddleware;
using JwtTokenMiddleware.RateLimiter;
using JwtTokenMiddleware.Redis;
using JwtTokenMiddleware.TokenRotation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddRedisServer("localhost:6379");
builder.Services.AddRateLimiter(new LimitingOptions
{
    FallbackMode = RateLimitFallbackMode.FailFast,
    UnauthorizedOptions = new LimitingPolicy
    {
        GeneralOption = new GeneralLimitingOption
        {
            MaxRequests = 10,
            WindowSpan = TimeSpan.FromMinutes(2)
        }
    },
    AuthorizedOptions = new LimitingPolicy
    {
        GeneralOption = new GeneralLimitingOption { MaxRequests = 100 },
        EndpointOptions =
        {
            new EndpointLimitingOption { Endpoint = "/api/login", MaxRequests = 5 },
            new EndpointLimitingOption { Endpoint = "/api/*", MaxRequests = 50 },
            new EndpointLimitingOption { Endpoint = "/api/users/{id}", MaxRequests = 20 }
        }
    }
});

builder.Services.AddJwtTokenRotation(new JwtTokenRotationOptions
{
    InactivityThreshold = 10,
    TokenExpiration = 120,
    //RedisConnectionString = "localhost:6379",
    JwtSecret = "YourSuperSecretKeyHere"
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimitingMiddleware();
app.UseJwtTokenRotation();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
