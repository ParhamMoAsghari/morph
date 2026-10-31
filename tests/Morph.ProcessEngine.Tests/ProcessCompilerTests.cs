using FluentAssertions;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Publishing;
using Xunit;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessCompilerTests
{
    [Fact]
    public void Compile_accepts_builtin_variable_and_string_steps()
    {
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: hello
            steps:
              - id: set
                type: variables.set
                with:
                  name: greeting
                  value: hello
              - id: concat
                type: string.concat
                with:
                  parts: ["{{greeting}}", " world"]
                  resultVar: out
            """);

        var result = new ProcessCompiler().Compile(doc, Guid.NewGuid());
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Compile_rejects_unknown_activity()
    {
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: bad
            steps:
              - id: x
                type: not.a.real.activity
            """);

        var result = new ProcessCompiler().Compile(doc, Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Unknown activity");
    }
}
