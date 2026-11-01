using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Extensions;
using OidcStarter.AspNetCore.Bff.Security;

namespace Backend.Auth;

internal static class OperatorWorkflowEndpoint
{
    public static IServiceCollection AddOperatorWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<OperatorAccess>();
        return services;
    }

    public static void MapOperatorWorkflow(this WebApplication app)
    {
        var policy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().Build();
        var routes = app.MapGroup("/api/demo/operator").RequireAuthorization(policy);
        routes.MapGet("/invitation-requests", ListAsync);
        routes.MapPost("/invitation-requests/{id}/approve", ApproveAsync);
        routes.MapPost("/invitation-requests/{id}/reject", RejectAsync);
        routes.MapPost("/invitations/{id}/revoke", RevokeAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, OperatorAccess access,
        SqliteInvitationStore store)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (await access.GetIdentityAsync(context) is null) return Results.StatusCode(403);
        if (context.Request.Query.Count > 1
            || context.Request.Query.Any(pair => pair.Key != "status" || pair.Value.Count != 1))
            return Results.BadRequest();
        var status = context.Request.Query.TryGetValue("status", out var value) ? value.ToString() : "pending";
        if (status is not ("pending" or "approved" or "rejected")) return Results.BadRequest();
        var records = await store.ListInvitationRequestsAsync(status, context.RequestAborted);
        return records is null ? Results.StatusCode(503) : Results.Ok(records);
    }

    private static Task<IResult> ApproveAsync(HttpContext context, string id, OperatorAccess access,
        SqliteInvitationStore store, IAntiforgery antiforgery, CsrfOriginValidator origin)
        => MutateAsync(context, id, access, antiforgery, origin,
            (parsed, operatorId) => store.ApproveRequestAsync(parsed, operatorId, context.RequestAborted));

    private static Task<IResult> RejectAsync(HttpContext context, string id, OperatorAccess access,
        SqliteInvitationStore store, IAntiforgery antiforgery, CsrfOriginValidator origin)
        => MutateAsync(context, id, access, antiforgery, origin,
            (parsed, operatorId) => store.RejectRequestAsync(parsed, operatorId, context.RequestAborted));

    private static Task<IResult> RevokeAsync(HttpContext context, string id, OperatorAccess access,
        SqliteInvitationStore store, IAntiforgery antiforgery, CsrfOriginValidator origin)
        => MutateAsync(context, id, access, antiforgery, origin,
            (parsed, _) => store.RevokeForOperatorAsync(parsed, context.RequestAborted));

    private static async Task<IResult> MutateAsync(HttpContext context, string id, OperatorAccess access,
        IAntiforgery antiforgery, CsrfOriginValidator origin,
        Func<Guid, string, Task<OperatorMutationResult>> mutation)
    {
        context.Response.Headers.CacheControl = "no-store";
        var operatorId = await access.GetIdentityAsync(context);
        if (operatorId is null) return Results.StatusCode(403);
        if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty) return Results.BadRequest();
        var request = context.Request;
        if (request.ContentLength is > 0 || request.ContentType is not null
            || request.Headers.TransferEncoding.Count > 0) return Results.BadRequest();
        if (request.ContentLength is null
            && await request.Body.ReadAsync(new byte[1], context.RequestAborted) != 0)
            return Results.BadRequest();
        if (!origin.IsTrustedOrigin(request)) return Results.StatusCode(403);
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Results.BadRequest(); }

        return await mutation(parsed, operatorId) switch
        {
            OperatorMutationResult.Success => Results.NoContent(),
            OperatorMutationResult.Missing => Results.NotFound(),
            OperatorMutationResult.Conflict => Results.Conflict(),
            _ => Results.StatusCode(503)
        };
    }
}

internal sealed class OperatorAccess
{
    private readonly HashSet<(string Issuer, string Subject)> allowed = [];
    private readonly bool validConfiguration;
    private readonly string? clientId;

    public OperatorAccess(IConfiguration configuration, IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
    {
        clientId = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme).ClientId;
        var entries = configuration.GetSection("DemoInvitations:Workflow:Operators").GetChildren().ToArray();
        validConfiguration = entries.Length > 0 && entries.All(entry =>
            entry.GetChildren().Count() == 2
            && Valid(entry["Issuer"]) && Valid(entry["Subject"])
            && Uri.TryCreate(entry["Issuer"], UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0
            && uri.Query.Length == 0 && uri.Fragment.Length == 0);
        if (validConfiguration)
            foreach (var entry in entries) allowed.Add((entry["Issuer"]!, entry["Subject"]!));
    }

    public async Task<string?> GetIdentityAsync(HttpContext context)
    {
        if (!validConfiguration || !Valid(clientId)) return null;
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Properties is null || result.Principal?.Identity?.IsAuthenticated != true
            || !result.Properties.TryGetOidcStarterLoginProviderId(out var provider) || provider != "oidc"
            || !result.Properties.TryGetOidcStarterValidatedOidcSignInMetadata(out var metadata)
            || metadata is null || metadata.ProviderId != "oidc"
            || metadata.AuthenticationScheme != OpenIdConnectDefaults.AuthenticationScheme
            || !Valid(metadata.Issuer) || !Valid(metadata.Subject)
            || metadata.ClientId != clientId) return null;

        var subjects = result.Principal.FindAll(ClaimTypes.NameIdentifier).Select(claim => claim.Value).ToArray();
        var rawSubjects = result.Principal.FindAll("sub").Select(claim => claim.Value).ToArray();
        var issuers = result.Principal.FindAll("iss").Select(claim => claim.Value).ToArray();
        if (subjects is not { Length: 1 } || subjects[0] != metadata.Subject
            || rawSubjects.Any(subject => subject != metadata.Subject)
            || rawSubjects.Length > 1 || issuers.Length > 1
            || issuers.Any(issuer => issuer != metadata.Issuer)
            || !allowed.Contains((metadata.Issuer, metadata.Subject))) return null;

        return $"{metadata.Issuer.Length}:{metadata.Issuer}{metadata.Subject.Length}:{metadata.Subject}";
    }

    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 512 && value == value.Trim() && !value.Any(char.IsControl)
        && !value.Contains('*');
}
