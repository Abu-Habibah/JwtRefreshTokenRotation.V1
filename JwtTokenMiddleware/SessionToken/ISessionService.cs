namespace TokenMiddleware.SessionToken;

public interface ISessionService
{
    Task<string> CreateSessionAsync(SessionPayload payload, TimeSpan ttl);
    Task<SessionPayload?> ValidateSessionAsync(string token, TimeSpan? slidingTtl = null);
    Task InvalidateSessionAsync(string token);
}
