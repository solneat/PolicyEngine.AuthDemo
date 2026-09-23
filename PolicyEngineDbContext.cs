using Microsoft.EntityFrameworkCore;
using SolNeat.PolicyEngine.EF;

namespace SolNeat.AuthDemo;

public class PolicyEngineDbContext : DbContext
{
    public PolicyEngineDbContext(DbContextOptions<PolicyEngineDbContext> options) : base(options) { }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.UseSolNeatPolicyEngine();
    }
}