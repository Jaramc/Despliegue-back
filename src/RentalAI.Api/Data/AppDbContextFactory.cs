using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RentalAI.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = MySqlConnectionString.Build(
            Environment.GetEnvironmentVariable("MYSQL_HOST"),
            Environment.GetEnvironmentVariable("MYSQL_PORT"),
            Environment.GetEnvironmentVariable("MYSQL_DATABASE"),
            Environment.GetEnvironmentVariable("MYSQL_USER"),
            Environment.GetEnvironmentVariable("MYSQL_PASSWORD"));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;

        return new AppDbContext(options);
    }
}
