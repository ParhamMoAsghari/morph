using FluentAssertions;
using Morph.DataPlane.Query;
using Npgsql;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

public sealed class CompiledSqlTests
{
    [Fact]
    public void CompiledSql_holds_sql_and_parameters()
    {
        var parameters = new List<NpgsqlParameter> { new("p0", "v") };
        var compiled = new CompiledSql
        {
            Sql = "WHERE \"c_title\" = @p0",
            Parameters = parameters
        };

        compiled.Sql.Should().Contain("@p0");
        compiled.Parameters.Should().HaveCount(1);
        compiled.Parameters[0].Value.Should().Be("v");
    }
}
