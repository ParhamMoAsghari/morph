using FluentAssertions;
using Morph.ProcessEngine.Ir;
using Xunit;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessIrSerializerTests
{
    [Fact]
    public void ParseJson_and_ToJson_round_trip()
    {
        const string json = """
            {
              "apiVersion": "morph/v1",
              "kind": "Process",
              "slug": "demo",
              "steps": [
                { "id": "a", "type": "variables.set", "with": { "name": "x", "value": 1 } }
              ]
            }
            """;

        var doc = ProcessIrSerializer.ParseJson(json);
        doc.Slug.Should().Be("demo");
        doc.Steps.Should().ContainSingle(s => s.Id == "a");

        var roundTrip = ProcessIrSerializer.ParseJson(ProcessIrSerializer.ToJson(doc));
        roundTrip.Slug.Should().Be("demo");
        roundTrip.Steps[0].Type.Should().Be("variables.set");
    }

    [Fact]
    public void ParseCompiled_round_trips_compiled_graph()
    {
        var compiled = new CompiledProcess
        {
            Slug = "x",
            VersionId = Guid.NewGuid(),
            Steps =
            [
                new CompiledStep { Id = "s1", Type = "variables.set" }
            ]
        };

        var json = ProcessIrSerializer.ToJson(compiled);
        var parsed = ProcessIrSerializer.ParseCompiled(json);

        parsed.Slug.Should().Be("x");
        parsed.Steps.Should().ContainSingle(s => s.Id == "s1");
    }

    [Fact]
    public void ParseJson_null_document_throws()
    {
        var act = () => ProcessIrSerializer.ParseJson("null");
        act.Should().Throw<InvalidOperationException>();
    }
}
