using FluentAssertions;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Persistence;
using Morph.EntityEngine.Publishing;
using Morph.EntityEngine.Services;
using Morph.EntityEngine.Validation;

namespace Morph.EntityEngine.Tests.TestSupport;

internal sealed class EntityEngineFixture
{
    public EntityEngineFixture()
    {
        Store = new InMemoryDefinitionStore();
        Clock = new SystemClock();
        Validator = new DefinitionGraphValidator(Store);
        Compiler = new EffectiveModelCompiler(Store, Validator, Clock);
        Composition = new CompositionOperations(Store, Clock);
        Decomposition = new DecompositionOperations(Store, Clock);
        Publisher = new DefinitionPublisher(Store, Compiler, Clock);
        Service = new EntityDefinitionService(
            Store,
            Validator,
            Compiler,
            Composition,
            Decomposition,
            Publisher,
            Clock);
        EnvironmentId = Guid.NewGuid();
    }

    public InMemoryDefinitionStore Store { get; }
    public SystemClock Clock { get; }
    public DefinitionGraphValidator Validator { get; }
    public EffectiveModelCompiler Compiler { get; }
    public CompositionOperations Composition { get; }
    public DecompositionOperations Decomposition { get; }
    public DefinitionPublisher Publisher { get; }
    public EntityDefinitionService Service { get; }
    public Guid EnvironmentId { get; }

    public static FieldDefinition Field(
        string slug,
        FieldDataType type = FieldDataType.String,
        Guid? fieldId = null,
        bool required = false) =>
        new()
        {
            FieldId = fieldId ?? Guid.NewGuid(),
            Slug = slug,
            DisplayName = slug,
            DataType = type,
            IsRequired = required
        };

    public async Task<(EntityDefinition Def, DefinitionVersion Ver)> CreatePublishedComponentAsync(
        string slug,
        params FieldDefinition[] fields)
    {
        var create = await Service.CreateAsync(
            EnvironmentId,
            slug,
            slug,
            DefinitionKind.Component,
            fields);
        create.IsSuccess.Should().BeTrue(create.Error?.Message);
        var publish = await Service.PublishAsync(create.Value.Version.VersionId);
        publish.IsSuccess.Should().BeTrue(publish.Error?.Message);
        return (create.Value.Definition, publish.Value);
    }

    public async Task<(EntityDefinition Def, DefinitionVersion Ver)> CreatePublishedEntityAsync(
        string slug,
        params FieldDefinition[] fields)
    {
        var create = await Service.CreateAsync(
            EnvironmentId,
            slug,
            slug,
            DefinitionKind.Entity,
            fields);
        create.IsSuccess.Should().BeTrue(create.Error?.Message);
        var publish = await Service.PublishAsync(create.Value.Version.VersionId);
        publish.IsSuccess.Should().BeTrue(publish.Error?.Message);
        return (create.Value.Definition, publish.Value);
    }
}
