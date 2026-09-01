using System.Data.Common;

namespace OroBI.Api.Migrations;

public static class MigrationConnectionFactory
{
    public static string CreateAdministrativeConnection(string connectionString)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        builder["Database"] = "postgres";
        return builder.ConnectionString;
    }
}
