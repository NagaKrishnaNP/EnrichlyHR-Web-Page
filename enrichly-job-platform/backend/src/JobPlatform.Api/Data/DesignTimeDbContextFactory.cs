using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPlatform.Api.Data;

/// <summary>
/// Used only by `dotnet ef migrations add` / `dotnet ef database update` at design time. The
/// connection string here doesn't need to point at a reachable database for "migrations add" -
/// EF only needs a MySqlServerVersion to know which SQL dialect to generate. It's only used for
/// "database update" run directly from the CLI (which this project doesn't rely on - the API
/// applies pending migrations itself on startup, see Program.cs).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=enrichly;User=enrichly_app;Password=devpassword;";

        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));

        return new AppDbContext(optionsBuilder.Options);
    }
}
