using JwtTokenMiddleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddJwtTokenRotation(new JwtTokenRotationOptions
{
    InactivityThreshold = 10,
    TokenExpiration = 120,
    RedisConnectionString = "localhost:6379",
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

app.UseJwtTokenRotation();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
