using OroBI.Api.Migrations;

namespace OroBI.Api.IntegrationTests.Migrations;

public sealed class MigrationConnectionFactoryTests
{
    [Fact]
    public void Creates_administrative_connection_for_postgres_database()
    {
        var result = MigrationConnectionFactory.CreateAdministrativeConnection(
            "Host=server;Port=5432;Database=orobi;Username=user;Password=password;Ssl Mode=Require");

        Assert.Contains("Database=postgres", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Host=server", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Handles_migration_mode_before_jwt_configuration()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OroBI.Api", "Program.cs"));

        Assert.True(program.IndexOf("args.Contains(\"--migrate\"", StringComparison.Ordinal) < program.IndexOf("GetRequiredSection(JwtOptions.SectionName)", StringComparison.Ordinal));
    }
}
