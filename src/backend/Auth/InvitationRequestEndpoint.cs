using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OidcStarter.AspNetCore.Bff.Security;

namespace Backend.Auth;

internal static class InvitationRequestEndpoint
{
    private const string RatePolicy = "demo-invitation-requests";
    private const int MaxBodyBytes = 1024;

    public static IServiceCollection AddInvitationRequestWorkflow(this IServiceCollection services, string? databasePath)
    {
        services.TryAddSingleton(new SqliteInvitationStore(databasePath!));
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return ValueTask.CompletedTask;
            };
        });
        return services;
    }

    public static void MapInvitationRequestWorkflow(this WebApplication app)
        => app.MapPost("/api/demo/invitation-requests", HandleAsync).RequireRateLimiting(RatePolicy);

    private static async Task<IResult> HandleAsync(HttpContext context, IAntiforgery antiforgery,
        CsrfOriginValidator originValidator, SqliteInvitationStore store)
    {
        var request = context.Request;
        if (!originValidator.IsTrustedOrigin(request)) return Results.StatusCode(403);
        if (!request.HasJsonContentType() || request.ContentLength > MaxBodyBytes)
            return Results.BadRequest();

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        var bytes = new byte[MaxBodyBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await request.Body.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBodyBytes) return Results.BadRequest();

        string? email;
        try
        {
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count));
            var properties = json.RootElement.ValueKind == JsonValueKind.Object
                ? json.RootElement.EnumerateObject().ToArray() : [];
            if (properties.Length != 1 || !properties[0].NameEquals("email")
                || properties[0].Value.ValueKind != JsonValueKind.String)
                return Results.BadRequest();
            email = properties[0].Value.GetString();
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        return await store.CreateInvitationRequestAsync(email, context.RequestAborted) switch
        {
            InvitationRequestResult.Accepted => Results.StatusCode(202),
            InvitationRequestResult.InvalidEmail => Results.BadRequest(),
            _ => Results.StatusCode(503)
        };
    }
}
