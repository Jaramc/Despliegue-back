using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace RentalAI.Api.Data;

public static class MySqlConnectionString
{
    public static string Build(IConfiguration configuration) =>
        Build(
            configuration["MYSQL_HOST"],
            configuration["MYSQL_PORT"],
            configuration["MYSQL_DATABASE"],
            configuration["MYSQL_USER"],
            configuration["MYSQL_PASSWORD"]);

    public static string Build(string? host, string? port, string? database, string? user, string? password)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = string.IsNullOrWhiteSpace(host) ? "localhost" : host,
            Port = uint.TryParse(port, out var parsedPort) ? parsedPort : 3306u,
            Database = string.IsNullOrWhiteSpace(database) ? "rentalai" : database,
            UserID = string.IsNullOrWhiteSpace(user) ? "rentalai" : user,
            Password = password ?? string.Empty
        };

        return builder.ConnectionString;
    }

    public static string BuildForHangfire(IConfiguration configuration)
    {
        var builder = new MySqlConnectionStringBuilder(Build(configuration))
        {
            AllowUserVariables = true,
            UseAffectedRows = false
        };

        return builder.ConnectionString;
    }
}
