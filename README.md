# JWT Inactivity Middleware 🔒

[![NuGet](https://img.shields.io/nuget/v/JwtInactivityMiddleware.svg)](https://www.nuget.org/packages/JwtInactivityMiddleware)
[![Build](https://github.com/Abu-Habibah/JwtInactivityMiddleware/actions/workflows/dotnet.yml/badge.svg)](https://github.com/yourusername/JwtInactivityMiddleware/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**JWT Inactivity Middleware** is a reusable ASP.NET Core package that enforces inactivity thresholds on JWT bearer tokens.  
Unlike standard JWT expiration (`exp`), this middleware tracks *last access time* in Redis and rejects tokens that exceed a configurable inactivity window.

---

## ✨ Features
- **Inactivity threshold enforcement**: Reject tokens idle longer than the configured duration.  
- **Redis-backed tracking**: Distributed cache ensures consistency across multiple API instances.  
- **Sliding expiration**: Active tokens remain valid as long as they’re used within the threshold.  
- **Configurable options**: Set inactivity threshold, Redis connection string, and JWT secret via `JwtTokenRotationOptions`.  
- **JWT generator service**: Issue tokens with `jti` claim for inactivity tracking.  

---
## ✈️ Usage
Register to service

```csharp
builder.Services.AddJwtTokenRotation(new JwtTokenRotationOptions
{
    InactivityThreshold = TimeSpan.FromMinutes(15),
    RedisConnectionString = "localhost:6379",
    JwtSecret = builder.Configuration["Jwt:Secret"]
});

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
 ├── JwtTokenRotationMiddleware.cs   # Core middleware logic
 ├── JwtTokenRotationOptions.cs      # Configurable options
 ├── JwtTokenGenerator.cs            # Token generator service
 ├── JwtInactivityExtensions.cs      # DI + middleware registration
 ├── JwtTokenMiddleware.csproj       # Library project file

JwtTokenMiddleware.Sample/
 ├── Program.cs                      # Demo API setup
 ├── Controllers/AuthController.cs   # Example login + token issuance
 ├── JwtTokenMiddleware.Sample.csproj

JwtTokenMiddleware.Test/
 ├── JwtTokenRotationMiddlewareTests.cs              # Unit tests for inactivity logic
 ├── JwtTokenMiddleware.Test.csproj
```
---
## 📦 Installation

Add the NuGet package (once published):

```bash
dotnet add package JwtInactivityMiddleware
