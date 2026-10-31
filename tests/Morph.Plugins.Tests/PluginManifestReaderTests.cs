using FluentAssertions;
using Morph.Plugins.Loading;
using Xunit;

namespace Morph.Plugins.Tests;

public sealed class PluginManifestReaderTests
{
    [Fact]
    public void Parse_valid_manifest_succeeds()
    {
        const string json = """
            {
              "id": "morph.soap",
              "version": "1.0.0",
              "displayName": "SOAP",
              "permissions": ["process.activities", "http.outbound"],
              "capabilities": ["soap.invoke"]
            }
            """;

        var manifest = PluginManifestReader.Parse(json);

        manifest.Id.Should().Be("morph.soap");
        manifest.Permissions.Should().Contain(new[] { "process.activities", "http.outbound" });
    }

    [Fact]
    public void Parse_unknown_permission_throws()
    {
        const string json = """
            {
              "id": "morph.bad",
              "version": "1.0.0",
              "displayName": "Bad",
              "permissions": ["process.activities", "root.shell"]
            }
            """;

        var act = () => PluginManifestReader.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown permission*root.shell*");
    }

    [Fact]
    public void Parse_missing_id_throws()
    {
        const string json = """
            {
              "version": "1.0.0",
              "displayName": "Missing Id",
              "permissions": []
            }
            """;

        var act = () => PluginManifestReader.Parse(json);

        act.Should().Throw<System.Text.Json.JsonException>()
            .WithMessage("*id*");
    }
}
