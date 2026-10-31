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

if (builder.Configuration.GetValue<bool>("DemoInvitations:EnforceGoogle"))
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

var app = builder.Build();

app.UseOidcStarterBff();

app.MapControllers();

app.Run();
