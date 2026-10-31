using FluentAssertions;
using Morph.DataPlane.Sql;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

public sealed class GovernedSqlParserTests
{
    [Fact]
    public void Allows_select_against_entity_slug()
    {
        var result = GovernedSqlParser.Parse("SELECT id, title FROM note WHERE title = @title LIMIT 10");
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Kind.Should().Be(GovernedSqlKind.Select);
        result.Value.PrimaryEntitySlug.Should().Be("note");
        result.Value.ParameterNames.Should().Contain("title");
        result.Value.HasLimit.Should().BeTrue();
        result.Value.TemplateSql.Should().Contain("{{entity:note}}");
    }

    [Fact]
    public void Denies_ddl_and_copy()
    {
        GovernedSqlParser.Parse("DROP TABLE note").IsFailure.Should().BeTrue();
        GovernedSqlParser.Parse("CREATE TABLE x (id int)").IsFailure.Should().BeTrue();
        GovernedSqlParser.Parse("COPY note TO STDOUT").IsFailure.Should().BeTrue();
        GovernedSqlParser.Parse("SELECT * FROM pg_catalog.pg_tables").IsFailure.Should().BeTrue();
        GovernedSqlParser.Parse("SELECT 1; SELECT 2").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Allows_insert_update_delete()
    {
        var insert = GovernedSqlParser.Parse("INSERT INTO note (title) VALUES (@title)");
        insert.IsSuccess.Should().BeTrue(insert.Error?.Message);

        var update = GovernedSqlParser.Parse("UPDATE e_note SET title = @title WHERE id = @id");
        update.IsSuccess.Should().BeTrue(update.Error?.Message);
        update.Value.Kind.Should().Be(GovernedSqlKind.Update);
        update.Value.PrimaryEntitySlug.Should().Be("note");

        var delete = GovernedSqlParser.Parse("DELETE FROM note WHERE id = @id");
        delete.IsSuccess.Should().BeTrue(delete.Error?.Message);
    }

    [Fact]
    public void Denies_session_set_but_not_update_set()
    {
        GovernedSqlParser.Parse("SET ROLE postgres").IsFailure.Should().BeTrue();
        GovernedSqlParser.Parse("UPDATE note SET title = @t WHERE id = @id").IsSuccess.Should().BeTrue();
    }
}
