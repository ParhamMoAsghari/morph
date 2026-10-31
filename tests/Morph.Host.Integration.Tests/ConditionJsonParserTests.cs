using System.Text.Json;
using FluentAssertions;
using Morph.Platform.Api.Access;
using Xunit;

namespace Morph.Host.Integration.Tests;

public sealed class ConditionJsonParserTests
{
    [Fact]
    public void Parses_eq_with_attr_and_literal()
    {
        using var doc = JsonDocument.Parse("""
            {"op":"eq","left":{"attr":"subject.sub"},"right":{"literal":"abc"}}
            """);
        var expr = ConditionJsonParser.Parse(doc.RootElement);
        expr.Op.Should().Be("eq");
        expr.Left!.IsAttribute.Should().BeTrue();
        expr.Left.AttributePath.Should().Be("subject.sub");
        expr.Right!.IsAttribute.Should().BeFalse();
        expr.Right.LiteralValue.Should().Be("abc");
    }

    [Fact]
    public void Parses_and_or_not_tree()
    {
        using var doc = JsonDocument.Parse("""
            {
              "and": [
                {"op":"eq","left":"subject.sub","right":{"literal":"u1"}},
                {
                  "or": [
                    {"op":"in","left":{"literal":"owner"},"right":{"attr":"subject.roles"}},
                    {"not":{"op":"eq","left":{"attr":"resource.status"},"right":{"literal":"archived"}}}
                  ]
                }
              ]
            }
            """);
        var expr = ConditionJsonParser.Parse(doc.RootElement);
        expr.Op.Should().Be("and");
        expr.Children.Should().NotBeNull();
        expr.Children!.Count.Should().Be(2);
        expr.Children[1].Op.Should().Be("or");
        expr.Children[1].Children![1].Op.Should().Be("not");
    }

    [Fact]
    public void Parses_string_operand_as_attr_when_dotted()
    {
        using var doc = JsonDocument.Parse("""
            {"op":"eq","left":"resource.owner_id","right":"plain"}
            """);
        var expr = ConditionJsonParser.Parse(doc.RootElement);
        expr.Left!.IsAttribute.Should().BeTrue();
        expr.Right!.IsAttribute.Should().BeFalse();
        expr.Right.LiteralValue.Should().Be("plain");
    }

    [Fact]
    public void Parses_bool_and_number_literals()
    {
        using var doc = JsonDocument.Parse("""
            {"op":"eq","left":{"attr":"resource.active"},"right":{"literal":true}}
            """);
        var expr = ConditionJsonParser.Parse(doc.RootElement);
        expr.Right!.LiteralValue.Should().Be(true);

        using var num = JsonDocument.Parse("""
            {"op":"gt","left":{"attr":"resource.n"},"right":{"literal":42}}
            """);
        var n = ConditionJsonParser.Parse(num.RootElement);
        n.Right!.LiteralValue.Should().Be(42L);
    }
}
