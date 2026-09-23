# SolNeat.PolicyEngine Quickstart Demo

This repository demonstrates how to integrate **SolNeat.PolicyEngine** with **OpenIddict** and **Entity Framework Core** to build an RFC 9068-compliant Identity Provider that evaluates fine-grained authorization policies (PAP/PDP/PEP) and injects active permissions into issued JWT access tokens.

---

## 🚀 Architecture Overview

1. **OpenIddict**: Handles OpenID Connect / OAuth 2.0 token generation and identity management across standard OAuth 2.0 / OIDC grant flows.
2. **SolNeat.PolicyEngine.EF**: Manages policy persistence, actor roles, resource definitions, and action scopes in a relational database (`SQLite`).
3. **SolNeat.PolicyEngine.OpenIddict**: Acts as a Policy Enforcement Point (PEP) bridge during token issuance, enriching the principal with the custom `x-access-permission` claim (`Resource:Action`).

---

## 💻 Integration & Setup

### 1. Application Pipeline (`Program.cs`)

```csharp
// Register PolicyEngine Persistence Context
builder.Services.AddDbContext<PolicyEngineDbContext>(opt => 
    opt.UseSqlite("Data Source=policy.db"));

// Attach PolicyEngine PEP Bridge to OpenIddict Token Pipeline
builder.Services.AddOpenIddict()
    .AddServer(opt =>
    {
        // Enriches issued access tokens with evaluated 'x-access-permission' claims
        opt.AddSolNeatPolicyEngine()
           .UseDbContext<PolicyEngineDbContext>();
    });


// Seed SolNeat.PolicyEngine (Actor -> Role -> Permissions)
var policyManager = scope.ServiceProvider.GetRequiredService<IPolicyManager>();

var actor = await policyManager.GetOrCreateActorAsync("postman-client", cancellationToken);
var role = await policyManager.CreateRoleAsync("worker", cancellationToken);

var permissions = new[] { ("users", "read"), ("orders", "create"), ("reports", "generate") };

foreach (var (resource, action) in permissions)
{
    var perm = await policyManager.CreatePermissionAsync(resource, action, cancellationToken);
    await policyManager.AssignPermissionToRoleAsync(role.Id, perm.Id, cancellationToken);
}

await policyManager.AssignRoleToActorAsync(actor.Id, role.Id, cancellationToken);
```

### 2. Run & Obtain Token
#### Start the Server
```bash
dotnet run
```

#### Request Access Token via cURL
```bash
curl -X POST "https://localhost:7080/connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=postman-client&client_secret=secret_key&scope=api" \
  --insecure
``` 

#### Inspect the Decoded JWT

Decode the resulting access_token to verify the generated x-access-permission array:

```csharp
{
  "iss": "https://localhost:7080/",
  "sub": "postman-client",
  "client_id": "postman-client",
  "x-access-permission": [
    "users:read",
    "orders:create",
    "reports:generate"
  ]
}



