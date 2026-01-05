# JWT Inactivity Middleware 🔒

[![NuGet](https://img.shields.io/nuget/v/JwtRefreshTokenRotation.svg)](https://www.nuget.org/packages/JwtRefreshTokenRotation)
[![CI](https://github.com/Abu-Habibah/JwtRefreshTokenRotation.V1/actions/workflows/ci.yml/badge.svg)](https://github.com/Abu-Habibah/JwtRefreshTokenRotation.V1/actions/workflows/ci.yml)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)


**JWT Inactivity Middleware** is a reusable ASP.NET Core package that enforces inactivity thresholds on JWT bearer tokens.  
Unlike standard JWT expiration (`exp`), this middleware tracks *last access time* in Redis and rejects tokens that exceed a configurable inactivity window.

---

## ✨ Features
- **Inactivity threshold enforcement**: Reject tokens idle longer than the configured duration.  
- **Redis-backed tracking**: Distributed cache ensures consistency across multiple API instances.  
- **Sliding expiration**: Active tokens remain valid as long as they’re used within the threshold.  
- **Configurable options**: Set inactivity threshold, Redis connection string, and JWT secret via `JwtTokenRotationOptions`.  
- **JWT generator service**: Issue tokens with `jti` claim for inactivity tracking.  
- **Fix Window Rate Limiter**: Optional rate limiting based on fixed time windows to control request rates.

---
## ✈️ Usage
Register to service

```csharp
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimitingMiddleware();
app.UseJwtTokenRotation();
````
---
## 🔒Best Practices
Always enforce both absolute expiration (exp) and inactivity threshold.

- Use Redis TTL to auto-clean expired sessions.
- Configure secrets via environment variables or appsettings.json.
- Log jti, user ID, and inactivity rejections for auditing.
- Keep middleware lightweight and stateless aside from Redis.

---
## 🎯 Contributing
Contributions are welcome!

- Fork the repo
- Create a feature branch
- Submit a pull request

* Please ensure all tests pass before submitting.
* For major changes, open an issue first to discuss what you’d like to change.
---

## 🛠 Project Structure
```csharp
JwtTokenMiddleware/
    RateLimiter/
         ├── ILimitingOption.cs                 # Limiting option interface
         ├── LimitingOptions.cs                 # Limiting options model
         ├── LimitingPolicy.cs                  # Limiting policy model
         ├── LimitingPolicyResolver.cs          # Limiting policy resolver
         ├── RateLimitFallbackMode.cs           # Limiting fallback mode enum
         ├── RateLimitingExtension.cs           # DI + middleware registration
         ├── RateLimitingMiddleware.cs          # Middleware logic
     Redis/
         ├── RedisConnectionExtention.cs        # DI registration for Redis IConnectionMultiplexer
     TokenRotation/
         ├── JwtTokenRotationMiddleware.cs      # Core middleware logic
         ├── JwtTokenRotationOptions.cs         # Configurable options
         ├── JwtTokenGenerator.cs               # Token generator service
         ├── JwtTokenRotationExtension.cs       # DI + middleware registration
         ├── JwtTokenMiddleware.csproj          # Library project file

JwtTokenMiddleware.Sample/
 ├── Program.cs                                 # Demo API setup
 ├── Controllers/AuthController.cs              # Example login + token issuance
 ├── JwtTokenMiddleware.Sample.csproj

JwtTokenMiddleware.Test/
 ├── JwtTokenRotationMiddlewareTests.cs         # Unit tests for inactivity logic
 ├── JwtTokenMiddleware.Test.csproj
```
----

## 🔌Sequence Diagram
```csharp
User                AuthController          JwtTokenGenerator        Redis                                                                      Middleware
 |                        |                        |                   |                                                                            |
 |--- Login Request ----->|                        |                   |                                                                            |
 |                        |--- GenerateTokenAsync->|                   |                                                                            |
 |                        |                        |--- Create JWT ----|                                                                            |
 |                        |                        |--- Store jti,lastAccess,TTL=TokenExpiration -------------------------------------------------->|
 |                        |<-- JWT Token ----------|                   |                                                                            |
 |<-- Token Response -----|                        |                   |                                                                            |
 |                                                                                                                                                  |
 |--- API Request -------->------------------------------------------------------->-----------------------------------------------------------------|
 |                        |                        |                   |                                                                            |
 |                        |                        |                   |--- RateLimiter: INCR key, EXPIRE window span ----------------------------->|
 |                        |                        |                   |<-- allowed/blocked, remaining, reset --------------------------------------|
 |                        |                        |                   |<-- X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset headers ----|
 |                        |                        |                   |                                                                            |
 |                        |                        |                   |--- Get jti,lastAccess ---------------------------------------------------->|
 |                        |                        |                   |<-- lastAccess,TTL ---------------------------------------------------------|
 |                        |                        |                   |--- Compare inactivity -----------------------------------------------------|
 |                        |                        |                   |--- If expired -> 401 ------------------------------------------------------|
 |                        |                        |                   |--- If valid: update lastAccess, TTL=remainingLifetime--------------------->|
 |                        |                        |                   |--- If extended: issue new token ------------------------------------------>|
 |                        |                        |                   |<-- X-New-Token header -----------------------------------------------------|
 |                        |                        |                   |                                                                            |
 |<-- Response (200/401/429) with headers ---------|                   |                                                                            |

```
---
## 📝 Change Log
    🏷️ 1.5.0 
        - Added 'fix window' rate limiter feature.
        - Limiting information is returned via response headers:
            "X-RateLimit-Limit" = Max request in a window span
            "X-RateLimit-Remaining" = remaining requests in the current window
            "X-RateLimit-Reset" = remaining time in seconds until the window span resets.
        - Redis server is now configurable via RedisConnectionExtension or you may register
          your own IConnectionMultiplexer instance.


    🏷️ 1.1.0 
        - Added auto extend expiration feature.
        - When expiration is extended, a new token with an updated jti 
          should be returned via the 'X-New-Token' response header.
        - Delete old jti from Redis when token is regenerated.
        - Improved jwt generation by using SecurityTokenDescriptor
          rather than JwtSecurityToken.

    🏷️ 1.0.0 
        - Initial release with core inactivity tracking features.

---
## 📦 Installation


```bash
dotnet add package JwtRefreshTokenRotation --version 1.5.0