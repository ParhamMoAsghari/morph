using FluentAssertions;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.DataPlane.Cache;
using Morph.Runtime.Api.Realtime;
using NSubstitute;
using Xunit;

namespace Morph.Messaging.Tests;

public sealed class RealtimeChannelAuthorizerTests
{
    [Fact]
    public async Task AuthorizeSubscribe_denies_anonymous()
    {
        var access = Substitute.For<IAccessEvaluator>();
        var envs = Substitute.For<IEnvironmentSchemaResolver>();
        var sut = new RealtimeChannelAuthorizer(access, envs);

        var result = await sut.AuthorizeSubscribeAsync(
            new AccessSubject { PrincipalId = "anonymous" },
            $"env:{Guid.NewGuid():D}:entity:employee");

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("Authentication");
        await access.DidNotReceiveWithAnyArgs().EvaluateAsync(default!);
    }

    [Fact]
    public async Task AuthorizeSubscribe_denies_invalid_channel()
    {
        var access = Substitute.For<IAccessEvaluator>();
        var envs = Substitute.For<IEnvironmentSchemaResolver>();
        var sut = new RealtimeChannelAuthorizer(access, envs);

        var result = await sut.AuthorizeSubscribeAsync(
            new AccessSubject { PrincipalId = "user-1", Roles = ["viewer"] },
            "not-a-channel");

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("Invalid channel");
    }

    [Fact]
    public async Task AuthorizeSubscribe_denies_when_access_denies()
    {
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var access = Substitute.For<IAccessEvaluator>();
        access.EvaluateAsync(Arg.Any<AccessContext>(), Arg.Any<CancellationToken>())
            .Returns(AccessDecision.Deny("no subscribe"));

        var envs = Substitute.For<IEnvironmentSchemaResolver>();
        envs.ResolveAsync(envId, Arg.Any<CancellationToken>())
            .Returns(new EnvironmentSchemaInfo(envId, projectId, "p_test"));

        var sut = new RealtimeChannelAuthorizer(access, envs);
        var result = await sut.AuthorizeSubscribeAsync(
            new AccessSubject { PrincipalId = "user-1" },
            RealtimeChannels.EntityChannel(envId, "employee"));

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be("no subscribe");
    }

    [Fact]
    public async Task AuthorizeSubscribe_allows_when_access_allows()
    {
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var access = Substitute.For<IAccessEvaluator>();
        access.EvaluateAsync(Arg.Any<AccessContext>(), Arg.Any<CancellationToken>())
            .Returns(AccessDecision.Allow("ok"));

        var envs = Substitute.For<IEnvironmentSchemaResolver>();
        envs.ResolveAsync(envId, Arg.Any<CancellationToken>())
            .Returns(new EnvironmentSchemaInfo(envId, projectId, "p_test"));

        var sut = new RealtimeChannelAuthorizer(access, envs);
        var result = await sut.AuthorizeSubscribeAsync(
            new AccessSubject { PrincipalId = "user-1", Roles = ["viewer"] },
            RealtimeChannels.EntityChannel(envId, "Employee"));

        result.Allowed.Should().BeTrue();
        result.EnvironmentId.Should().Be(envId);
        result.EntitySlug.Should().Be("employee");
        result.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task AuthorizeSubscribe_denies_when_environment_not_found()
    {
        var envId = Guid.NewGuid();
        var access = Substitute.For<IAccessEvaluator>();
        var envs = Substitute.For<IEnvironmentSchemaResolver>();
        envs.ResolveAsync(envId, Arg.Any<CancellationToken>()).Returns((EnvironmentSchemaInfo?)null);

        var sut = new RealtimeChannelAuthorizer(access, envs);
        var result = await sut.AuthorizeSubscribeAsync(
            new AccessSubject { PrincipalId = "user-1", Roles = ["viewer"] },
            RealtimeChannels.EntityChannel(envId, "employee"));

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("Environment not found");
        await access.DidNotReceiveWithAnyArgs().EvaluateAsync(default!);
    }

    [Fact]
    public void TryParseEntityChannel_round_trips()
    {
        var envId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var channel = RealtimeChannels.EntityChannel(envId, "Person");

        RealtimeChannels.TryParseEntityChannel(channel, out var parsedEnv, out var slug).Should().BeTrue();
        parsedEnv.Should().Be(envId);
        slug.Should().Be("person");
    }
}
