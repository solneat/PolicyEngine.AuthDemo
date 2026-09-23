using OpenIddict.Abstractions;
using SolNeat.PolicyEngine;

namespace SolNeat.AuthDemo;

public class DbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DbInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var oidcDb = scope.ServiceProvider.GetRequiredService<OpenIddictContext>();

        await oidcDb.Database.EnsureCreatedAsync(cancellationToken);

        var policyDb = scope.ServiceProvider.GetRequiredService<PolicyEngineDbContext>();

        await policyDb.Database.EnsureCreatedAsync(cancellationToken);


        var policyManager = scope.ServiceProvider.GetRequiredService<IPolicyManager>();

        var actor = await policyManager.GetOrCreateActorAsync("postman-client", cancellationToken);

        var role = await policyManager.CreateRoleAsync("worker", cancellationToken);

        var permissionsToCreate = new (string Resource, string Action)[]
        {
            ("users", "read"),
            ("users", "create"),
            ("users", "update"),
            ("users", "delete"),
            ("orders", "read"),
            ("orders", "create"),
            ("orders", "cancel"),
            ("reports", "generate"),
            ("reports", "download"),
            ("settings", "manage")
        };

        foreach (var (resource, action) in permissionsToCreate)
        {
            var permission = await policyManager.CreatePermissionAsync(resource, action, cancellationToken);
            await policyManager.AssignPermissionToRoleAsync(role.Id, permission.Id, cancellationToken);
        }

        await policyManager.AssignRoleToActorAsync(actor.Id, role.Id, cancellationToken);

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync("postman-client", cancellationToken) == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "postman-client",
                ClientSecret = "secret_key",
                DisplayName = "Postman / HTTP Client",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            }, cancellationToken);
        }

        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        if (await scopeManager.FindByNameAsync("api", cancellationToken) == null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "api",
                DisplayName = "API Access",
                Resources = { "resource_server" } // опционально: имя вашей API ресурса
            }, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}