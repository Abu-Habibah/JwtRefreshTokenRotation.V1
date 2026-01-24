using Microsoft.AspNetCore.Mvc;
using TokenMiddleware.JwtToken;

namespace JwtTokenMiddleware.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenGenerator _jwtGenerator;

    public AuthController(JwtTokenGenerator jwtGenerator)
    {
        _jwtGenerator = jwtGenerator;
    }

    /// <summary>
    /// Simulated login endpoint.
    /// Normally you would validate user credentials here.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // In a real app, validate username/password against a database
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("UserId is required");
        }

        var token = _jwtGenerator.GenerateTokenAsync(request.UserId);
        return Ok(new { token });
    }

    /// <summary>
    /// Protected endpoint to test middleware.
    /// Requires a valid JWT with activity tracking.
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok("Token is active and request passed inactivity middleware");
    }
}

public record LoginRequest(string UserId);
