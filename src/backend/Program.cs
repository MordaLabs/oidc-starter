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

var app = builder.Build();

app.UseOidcStarterBff();

app.MapControllers();

app.Run();
