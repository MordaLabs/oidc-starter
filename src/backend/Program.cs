using Backend.Configuration;
using Backend.Auth;
using OidcStarter.AspNetCore.Bff.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<StarterOptions>(
    builder.Configuration.GetSection(StarterOptions.SectionName));
builder.Services.AddOidcStarterRoleMapper<KeycloakRoleMapper>();
builder.Services.AddOidcStarterBff(builder.Configuration);

var googleSection = builder.Configuration.GetSection("ExternalLogin:Google");
if (googleSection.GetValue<bool>("Enabled"))
{
    builder.Services.AddOidcStarterGoogle(googleSection.GetSection("Options"));
}

var enforceGoogle = builder.Configuration.GetValue<bool>("DemoInvitations:EnforceGoogle");
if (enforceGoogle)
{
    builder.Services.AddGoogleInvitationGate(builder.Configuration["DemoInvitations:DatabasePath"]);
}

var facebookSection = builder.Configuration.GetSection("ExternalLogin:Facebook");
if (facebookSection.GetValue<bool>("Enabled"))
{
    builder.Services.AddOidcStarterFacebook(facebookSection.GetSection("Options"));
}

var githubSection = builder.Configuration.GetSection("ExternalLogin:GitHub");
if (githubSection.GetValue<bool>("Enabled"))
{
    builder.Services.AddOidcStarterGitHub(githubSection.GetSection("Options"));
}

var enforceGitHub = builder.Configuration.GetValue<bool>("DemoInvitations:EnforceGitHub");
if (enforceGitHub)
{
    builder.Services.AddGitHubInvitationGate(builder.Configuration["DemoInvitations:DatabasePath"]);
}

builder.Services.AddInvitationSessionValidation(
    builder.Configuration["DemoInvitations:DatabasePath"], enforceGoogle, enforceGitHub);

var invitationWorkflowEnabled = builder.Configuration.GetValue<bool>("DemoInvitations:Workflow:Enabled");
if (invitationWorkflowEnabled)
{
    builder.Services.AddInvitationRequestWorkflow(builder.Configuration["DemoInvitations:DatabasePath"]);
}

var app = builder.Build();

app.UseOidcStarterBff();
if (invitationWorkflowEnabled) app.UseRateLimiter();

app.MapControllers();
if (invitationWorkflowEnabled) app.MapInvitationRequestWorkflow();

app.Run();
