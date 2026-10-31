using FluentAssertions;
using Morph.DataPlane.Mapping;
using Morph.DataPlane.Query;
using Morph.EntityEngine.Domain;
using NpgsqlTypes;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

public sealed class FilterCompilerTests
{
    private static FilterCompiler CreateCompiler() =>
        new(new FieldColumnResolver(TestMapping(), TestModel()));

    [Fact]
    public void Compile_null_filter_returns_TRUE()
    {
        var result = CreateCompiler().Compile(null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Be("TRUE");
        result.Value.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Compile_and_or_not_composes_sql()
    {
        var filter = new FilterAnd(
        [
            new FilterComparison("title", FilterOperator.Eq, "a"),
            new FilterOr(
            [
                new FilterNot(new FilterComparison("title", FilterOperator.Eq, "b")),
                new FilterComparison("score", FilterOperator.Gt, 10)
            ])
        ]);

        var result = CreateCompiler().Compile(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Contain(" AND ");
        result.Value.Sql.Should().Contain(" OR ");
        result.Value.Sql.Should().Contain("NOT (");
    }

    [Theory]
    [InlineData(FilterOperator.Eq, "=")]
    [InlineData(FilterOperator.Neq, "<>")]
    [InlineData(FilterOperator.Gt, ">")]
    [InlineData(FilterOperator.Lt, "<")]
    public void Compile_comparison_operators(FilterOperator op, string sqlOp)
    {
        var filter = new FilterComparison("title", op, "x");
        var result = CreateCompiler().Compile(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Contain($" {sqlOp} @p");
    }

    [Fact]
    public void Compile_in_expands_parameters()
    {
        var filter = new FilterComparison("title", FilterOperator.In, new List<object?> { "a", "b" });
        var result = CreateCompiler().Compile(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Contain(" IN (");
        result.Value.Parameters.Should().HaveCount(2);
    }

    [Fact]
    public void Compile_in_empty_array_returns_FALSE()
    {
        var filter = new FilterComparison("title", FilterOperator.In, new List<object?>());
        var result = CreateCompiler().Compile(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Be("FALSE");
    }

    [Fact]
    public void Compile_contains_uses_ilike_wildcard()
    {
        var filter = new FilterComparison("title", FilterOperator.Contains, "needle");
        var result = CreateCompiler().Compile(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Contain(" ILIKE @p");
        result.Value.Parameters[0].Value.Should().Be("%needle%");
    }

    [Fact]
    public void Compile_is_null_and_is_not_null()
    {
        var nullFilter = CreateCompiler().Compile(new FilterComparison("title", FilterOperator.IsNull, null));
        var notNullFilter = CreateCompiler().Compile(new FilterComparison("title", FilterOperator.IsNotNull, null));

        nullFilter.Value.Sql.Should().Contain(" IS NULL");
        notNullFilter.Value.Sql.Should().Contain(" IS NOT NULL");
    }

    [Fact]
    public void Compile_unknown_field_returns_validation_failure()
    {
        var result = CreateCompiler().Compile(new FilterComparison("missing", FilterOperator.Eq, 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Unknown filter field");
    }

    [Fact]
    public void Compile_in_with_string_value_fails_validation()
    {
        var result = CreateCompiler().Compile(new FilterComparison("title", FilterOperator.In, "not-an-array"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("IN operator requires an array");
    }

    [Fact]
    public void CompileSort_default_when_null_or_empty()
    {
        var compiler = CreateCompiler();
        var defaultOrder = "\"created_at\" DESC, \"id\" DESC";

        compiler.CompileSort(null).Value.Sql.Should().Be(defaultOrder);
        compiler.CompileSort([]).Value.Sql.Should().Be(defaultOrder);
    }

    [Fact]
    public void CompileSort_builds_order_with_trailing_id()
    {
        var sorts = new List<SortSpec>
        {
            new("title", Descending: false),
            new("score", Descending: true)
        };

        var result = CreateCompiler().CompileSort(sorts);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sql.Should().Contain("\"c_title\" ASC");
        result.Value.Sql.Should().Contain("\"c_score\" DESC");
        result.Value.Sql.Should().EndWith("\"id\" DESC");
    }

    [Fact]
    public void CompileSort_rejects_unknown_field_and_too_many_sorts()
    {
        var compiler = CreateCompiler();

        compiler.CompileSort([new SortSpec("nope")]).IsFailure.Should().BeTrue();

        var many = Enumerable.Range(0, 6).Select(i => new SortSpec("title")).ToList();
        compiler.CompileSort(many).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddParam_maps_common_types()
    {
        var filter = new FilterAnd(
        [
            new FilterComparison("title", FilterOperator.Eq, "text"),
            new FilterComparison("score", FilterOperator.Eq, 42),
            new FilterComparison("id", FilterOperator.Eq, Guid.Parse("11111111-1111-1111-1111-111111111111"))
        ]);

        var result = CreateCompiler().Compile(filter);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);

        result.Value.Parameters.Should().Contain(p =>
            p.NpgsqlDbType == NpgsqlDbType.Text && (string?)p.Value == "text");
        result.Value.Parameters.Should().Contain(p =>
            (p.NpgsqlDbType == NpgsqlDbType.Integer || p.NpgsqlDbType == NpgsqlDbType.Bigint)
            && Convert.ToInt64(p.Value) == 42);
        result.Value.Parameters.Should().Contain(p => p.NpgsqlDbType == NpgsqlDbType.Uuid);
    }

    private static EffectiveModel TestModel()
    {
        var defId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var verId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var envId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var fieldId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        return new EffectiveModel
        {
            DefinitionId = defId,
            VersionId = verId,
            EnvironmentId = envId,
            Slug = "item",
            Kind = DefinitionKind.Entity,
            VersionNumber = 1,
            CompiledAt = DateTimeOffset.UtcNow,
            Fingerprint = "test",
            Components = [],
            Relationships = [],
            PinnedDependencies = [],
            Fields =
            [
                new EffectiveField
                {
                    FieldId = fieldId,
                    Path = "title",
                    Slug = "title",
                    DisplayName = "Title",
                    DataType = FieldDataType.String,
                    IsIndexed = true,
                    SourceDefinitionId = defId,
                    SourceVersionId = verId,
                    SourcePaths = ["title"]
                },
                new EffectiveField
                {
                    FieldId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    Path = "score",
                    Slug = "score",
                    DisplayName = "Score",
                    DataType = FieldDataType.Int,
                    IsIndexed = true,
                    SourceDefinitionId = defId,
                    SourceVersionId = verId,
                    SourcePaths = ["score"]
                }
            ]
        };
    }

    private static StorageMapping TestMapping()
    {
        var defId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var verId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var envId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        return new StorageMapping
        {
            DefinitionId = defId,
            VersionId = verId,
            EnvironmentId = envId,
            SchemaName = "public",
            TableName = "item",
            QualifiedTableName = "public.item",
            MappedAt = DateTimeOffset.UtcNow,
            Fingerprint = "map",
            Indexes = [],
            Columns =
            [
                new MappedColumn
                {
                    ColumnName = "c_title",
                    Kind = StorageColumnKind.TypedField,
                    PostgresType = "text",
                    FieldId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    FieldPath = "title"
                },
                new MappedColumn
                {
                    ColumnName = "c_score",
                    Kind = StorageColumnKind.TypedField,
                    PostgresType = "integer",
                    FieldId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    FieldPath = "score"
                }
            ]
        };
    }
}
