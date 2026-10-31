using System.Text.Json;
using FluentAssertions;
using Morph.Access.Domain;
using Morph.Platform.Api.Access;
using Xunit;

namespace Morph.Access.Tests;

public sealed class ConditionJsonParserTests
{
    [Fact]
    public void Parse_nested_and_or_not()
    {
        using var doc = JsonDocument.Parse("""
            {
              "and": [
                { "op": "eq", "left": { "attr": "subject.sub" }, "right": { "literal": "alice" } },
                { "or": [
                    { "not": { "op": "eq", "left": { "attr": "resource.type" }, "right": { "literal": "secret" } } },
                    { "op": "eq", "left": "resource.type", "right": "task" }
                  ]}
              ]
            }
            """);

        var expr = ConditionJsonParser.Parse(doc.RootElement);
        expr.Op.Should().Be("and");
        expr.Children.Should().HaveCount(2);
        expr.Children![1].Op.Should().Be("or");
    }

    [Fact]
    public void Parse_operand_attr_literal_and_string_shorthand()
    {
        using var doc = JsonDocument.Parse("""
            {
              "op": "eq",
              "left": { "attr": "subject.roles" },
              "right": { "literal": 42 }
            }
            """);

        var expr = ConditionJsonParser.Parse(doc.RootElement);
        expr.Left!.IsAttribute.Should().BeTrue();
        expr.Right!.IsAttribute.Should().BeFalse();
        expr.Right.LiteralValue.Should().Be(42L);
    }

    [Fact]
    public void Parse_requires_op_property_for_leaf_conditions()
    {
        var act = () =>
        {
            using var doc = JsonDocument.Parse("""{ "left": "subject.sub", "right": "bob" }""");
            ConditionJsonParser.Parse(doc.RootElement);
        };

        act.Should().Throw<KeyNotFoundException>();
    }
}
