using FluentAssertions;
using Morph.DataPlane.Query;
using Morph.Runtime.Api.Data;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

public sealed class FilterJsonParserTests
{
    [Fact]
    public void Parse_comparison_defaults_op_to_eq()
    {
        var node = FilterJsonParser.Parse("""{"field":"title","value":"hello"}""");

        node.Should().BeOfType<FilterComparison>();
        var cmp = (FilterComparison)node;
        cmp.FieldPath.Should().Be("title");
        cmp.Operator.Should().Be(FilterOperator.Eq);
        cmp.Value.Should().Be("hello");
    }

    [Fact]
    public void Parse_supports_operator_aliases()
    {
        var neq = (FilterComparison)FilterJsonParser.Parse("""{"field":"x","op":"!=","value":1}""");
        neq.Operator.Should().Be(FilterOperator.Neq);

        var contains = (FilterComparison)FilterJsonParser.Parse("""{"field":"x","op":"ilike","value":"a"}""");
        contains.Operator.Should().Be(FilterOperator.Contains);

        var isNull = (FilterComparison)FilterJsonParser.Parse("""{"field":"x","op":"is_null"}""");
        isNull.Operator.Should().Be(FilterOperator.IsNull);
    }

    [Fact]
    public void Parse_logical_combinators()
    {
        var node = FilterJsonParser.Parse("""
            {
              "and": [
                { "field": "a", "op": "eq", "value": 1 },
                { "or": [
                    { "not": { "field": "b", "op": "eq", "value": 2 } },
                    { "field": "c", "op": "in", "value": ["x","y"] }
                  ]}
              ]
            }
            """);

        node.Should().BeOfType<FilterAnd>();
        var and = (FilterAnd)node;
        and.Children.Should().HaveCount(2);
        and.Children[1].Should().BeOfType<FilterOr>();
    }

    [Fact]
    public void Parse_rejects_unknown_operator()
    {
        var act = () => FilterJsonParser.Parse("""{"field":"x","op":"regex","value":".*"}""");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not allowlisted*");
    }

    [Fact]
    public void Parse_missing_field_throws_key_not_found()
    {
        var act = () => FilterJsonParser.Parse("""{"op":"eq","value":1}""");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Parse_coerces_array_and_boolean_values()
    {
        var cmp = (FilterComparison)FilterJsonParser.Parse("""
            {"field":"tags","op":"in","value":[1,"two",true]}
            """);

        cmp.Value.Should().BeAssignableTo<IList<object?>>();
        var list = (IList<object?>)cmp.Value!;
        list[0].Should().Be(1L);
        list[1].Should().Be("two");
        list[2].Should().Be("true");
    }
}
