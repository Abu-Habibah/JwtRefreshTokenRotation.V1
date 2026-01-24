namespace TokenMiddleware.SessionToken;

public record SessionPayload(
    string UserId,
    string Role,
    string Policy,
    IDictionary<string, string>? ExtraClaims = null
);
