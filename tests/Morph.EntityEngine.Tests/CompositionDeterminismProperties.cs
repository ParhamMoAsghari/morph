using CsCheck;
using FluentAssertions;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Tests.TestSupport;
using Xunit;

namespace Morph.EntityEngine.Tests;

public sealed class CompositionDeterminismProperties
{
    [Fact]
    public void Compile_is_deterministic_for_same_authoring_graph()
    {
        Gen.Select(
                Gen.String[Gen.Char.AlphaNumeric, 1, 8],
                Gen.Int[1, 5],
                Gen.Int[0, 3])
            .Sample(tuple =>
            {
                var (slugSeed, fieldCount, extraLocal) = tuple;
                Run(async () =>
                {
                    var fx = new EntityEngineFixture();

                    var fields = Enumerable.Range(0, fieldCount)
                        .Select(i => EntityEngineFixture.Field(
                            $"f{i}_{slugSeed}",
                            fieldId: CreateDeterministicGuid(slugSeed, i)))
                        .ToArray();

                    var (_, componentV) = await fx.CreatePublishedComponentAsync($"cmp_{slugSeed}", fields);

                    var local = Enumerable.Range(0, extraLocal)
                        .Select(i => EntityEngineFixture.Field(
                            $"local{i}",
                            fieldId: CreateDeterministicGuid(slugSeed, 100 + i)))
                        .ToList();

                    var compose = await fx.Service.ComposeAsync(
                        fx.EnvironmentId,
                        $"entity_{slugSeed}",
                        $"Entity {slugSeed}",
                        DefinitionKind.Entity,
                        [
                            new ComponentUse
                            {
                                ComponentUseId = CreateDeterministicGuid(slugSeed, 999),
                                Alias = "cmp",
                                TargetDefinitionId = componentV.DefinitionId,
                                PinnedVersionId = componentV.VersionId,
                                Mode = ComponentIncludeMode.Embed
                            }
                        ],
                        local);

                    compose.IsSuccess.Should().BeTrue(compose.Error?.Message);

                    var first = await fx.Compiler.CompileAsync(compose.Value.Version);
                    var second = await fx.Compiler.CompileAsync(compose.Value.Version);

                    first.IsSuccess.Should().BeTrue(first.Error?.Message);
                    second.IsSuccess.Should().BeTrue(second.Error?.Message);
                    second.Value.Fingerprint.Should().Be(first.Value.Fingerprint);
                    second.Value.Fields.Select(f => (f.FieldId, f.Path))
                        .Should().Equal(first.Value.Fields.Select(f => (f.FieldId, f.Path)));
                });
            });
    }

    [Fact]
    public void Cycles_are_always_rejected()
    {
        Gen.Int[2, 4].Sample(nodeCount =>
        {
            Run(async () =>
            {
                var fx = new EntityEngineFixture();
                var drafts = new List<(EntityDefinition Def, DefinitionVersion Ver)>();

                for (var i = 0; i < nodeCount; i++)
                {
                    var created = await fx.Service.CreateAsync(
                        fx.EnvironmentId,
                        $"n{i}",
                        $"N{i}",
                        DefinitionKind.Component);
                    created.IsSuccess.Should().BeTrue();
                    drafts.Add(created.Value);
                }

                for (var i = 0; i < nodeCount; i++)
                {
                    var next = drafts[(i + 1) % nodeCount];
                    await fx.Service.UpdateDraftAsync(drafts[i].Ver.VersionId, draft =>
                    {
                        draft.ComponentUses.Add(new ComponentUse
                        {
                            ComponentUseId = Guid.NewGuid(),
                            Alias = $"n{(i + 1) % nodeCount}",
                            TargetDefinitionId = next.Def.DefinitionId,
                            PinnedVersionId = next.Ver.VersionId,
                            Mode = ComponentIncludeMode.Embed
                        });
                    });
                }

                var issues = await fx.Service.ValidateAsync(drafts[0].Ver.VersionId);
                issues.Should().Contain(i => i.Code == "composition_cycle");
            });
        });
    }

    private static void Run(Func<Task> action) => action().GetAwaiter().GetResult();

    private static Guid CreateDeterministicGuid(string seed, int index)
    {
        var bytes = new byte[16];
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{seed}:{index}"));
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }
}
