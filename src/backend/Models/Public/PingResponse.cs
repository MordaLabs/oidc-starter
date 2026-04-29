namespace Backend.Models.Public;

public sealed record PingResponse(
    string Status,
    string ApplicationName,
    DateTimeOffset TimestampUtc,
    bool OidcConfigured);
