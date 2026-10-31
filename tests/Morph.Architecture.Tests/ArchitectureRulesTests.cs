using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Morph.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void BuildingBlocks_should_not_depend_on_other_Morph_projects()
    {
        var result = Types.InAssembly(typeof(Morph.BuildingBlocks.Results.Result).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.EntityEngine",
                "Morph.Access",
                "Morph.DataPlane",
                "Morph.Files",
                "Morph.Messaging",
                "Morph.Mail",
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Plugins",
                "Morph.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void Files_should_not_depend_on_Api_Infrastructure_or_Host()
    {
        var result = Types.InAssembly(typeof(Morph.Files.Services.FileService).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host",
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.EntityEngine",
                "Morph.DataPlane")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void Messaging_should_not_depend_on_Api_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Morph.Messaging.Outbox.IOutboxPublisher).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host",
                "Morph.Files",
                "Morph.Mail")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void Access_should_not_depend_on_Api_Infrastructure_or_EntityEngine()
    {
        var result = Types.InAssembly(typeof(Morph.Access.Evaluation.IAccessEvaluator).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.EntityEngine",
                "Morph.DataPlane",
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void DataPlane_should_not_depend_on_Api_or_Host()
    {
        var result = Types.InAssembly(typeof(Morph.DataPlane.Services.DataPlaneService).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void PluginsAbstractions_should_not_depend_on_Host_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Morph.Plugins.Abstractions.IMorphPlugin).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Plugins",
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.EntityEngine",
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void EntityEngine_should_not_depend_on_Api_Infrastructure_or_Host()
    {
        var result = Types.InAssembly(typeof(Morph.EntityEngine.Domain.DefinitionKind).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.Access",
                "Morph.DataPlane",
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void Tenancy_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(typeof(Morph.Tenancy.Organizations.Organization).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void Identity_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(typeof(Morph.Identity.Users.PlatformUser).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host",
                "Morph.Tenancy")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void RuntimeApi_should_not_depend_on_PlatformApi()
    {
        var result = Types.InAssembly(typeof(Morph.Runtime.Api.RuntimeApiExtensions).Assembly)
            .Should()
            .NotHaveDependencyOn("Morph.Platform.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void ProcessEngine_should_not_depend_on_Api_or_Host()
    {
        var result = Types.InAssembly(typeof(Morph.ProcessEngine.Domain.ProcessDefinition).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host",
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.Functions.Runtime")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    [Fact]
    public void FunctionsRuntime_should_not_depend_on_Host_or_PlatformApi()
    {
        var result = Types.InAssembly(typeof(Morph.Functions.Runtime.Domain.FunctionDefinition).Assembly)
            .Should()
            .NotHaveDependencyOnAll(
                "Morph.Infrastructure",
                "Morph.Platform.Api",
                "Morph.Runtime.Api",
                "Morph.Host",
                "Morph.Identity",
                "Morph.Tenancy",
                "Morph.ProcessEngine")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Explain(result));
    }

    private static string Explain(TestResult result)
    {
        if (result.IsSuccessful || result.FailingTypes is null)
        {
            return "Architecture rule failed.";
        }

        return "Violations: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName));
    }
}
