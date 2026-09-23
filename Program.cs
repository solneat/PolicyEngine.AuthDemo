using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using SolNeat.AuthDemo;
using SolNeat.PolicyEngine.EF;
using SolNeat.PolicyEngine.OpenIddict;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OpenIddictContext>(options =>
{
    options.UseSqlite("Data Source=auth.db");
});

builder.Services.AddDbContext<PolicyEngineDbContext>(options =>
{
    options.UseSqlite("Data Source=policy.db");
});


builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<OpenIddictContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");

        options.AllowClientCredentialsFlow();

        options.AddDevelopmentEncryptionCertificate();

        options.AddDevelopmentSigningCertificate();

        options.UseAspNetCore().DisableTransportSecurityRequirement();

        options.UseAspNetCore().EnableTokenEndpointPassthrough();

        options.DisableAccessTokenEncryption();

        options.AcceptAnonymousClients();

        options.AddSolNeatPolicyEngine().UseDbContext<PolicyEngineDbContext>();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddHostedService<DbInitializer>();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapPost("/connect/token", async (HttpContext context, IOpenIddictApplicationManager applicationManager) =>
{
    var request = context.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("Запрос OpenIddict не может быть получен.");

    if (request.IsClientCredentialsGrantType())
    {
        var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("Клиент не найден.");

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(
            OpenIddictConstants.Claims.Subject,
            (await applicationManager.GetClientIdAsync(application))!,
            OpenIddictConstants.Destinations.AccessToken);

        identity.AddClaim(
            OpenIddictConstants.Claims.Name,
            (await applicationManager.GetDisplayNameAsync(application))!,
            OpenIddictConstants.Destinations.AccessToken);

        var principal = new ClaimsPrincipal(identity);
        
        principal.SetScopes(request.GetScopes());

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    return Results.BadRequest(new OpenIddictResponse
    {
        Error = OpenIddictConstants.Errors.UnsupportedGrantType,
        ErrorDescription = "grant_type not supported."
    });
});

app.Run();