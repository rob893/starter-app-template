using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StarterApp.API.Data;

/// <summary>
/// Design-time factory for creating <see cref="DataContext"/> instances.
/// Used by EF Core tools (migrations, scaffolding) when no host is available.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    /// <inheritdoc />
    public DataContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql("Host=localhost;Database=starterapp;Username=postgres;Password=postgres")
            .Options;

        return new DataContext(options);
    }
}
