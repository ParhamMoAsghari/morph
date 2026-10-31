using FluentAssertions;
using Morph.BuildingBlocks.Paging;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Security;
using Morph.BuildingBlocks.Time;
using Xunit;

namespace Morph.BuildingBlocks.Tests;

public sealed class ResultAndPagingTests
{
    [Fact]
    public void Result_success_and_failure_paths()
    {
        Result.Success().IsSuccess.Should().BeTrue();
        Result.Failure(Error.Validation("bad")).IsFailure.Should().BeTrue();

        var ok = Result.Success(42);
        ok.Value.Should().Be(42);

        var fail = Result.Failure<int>(Error.NotFound("missing"));
        fail.IsFailure.Should().BeTrue();
        var act = () => _ = fail.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_factories_set_codes()
    {
        Error.Validation("v").Code.Should().Be("validation");
        Error.NotFound("n").Code.Should().Be("not_found");
        Error.Conflict("c").Code.Should().Be("conflict");
        Error.Failure("f").Code.Should().Be("failure");
        Error.Forbidden("x").Code.Should().Be("forbidden");
    }

    [Fact]
    public void PageRequest_and_PageResult_work()
    {
        var req = new PageRequest(25, "c1");
        req.PageSize.Should().Be(25);
        req.Cursor.Should().Be("c1");

        var page = new PageResult<string>(["a"], "n", true);
        page.Items.Should().ContainSingle("a");
        page.HasMore.Should().BeTrue();
        page.NextCursor.Should().Be("n");
    }

    [Fact]
    public void SystemClock_returns_utc()
    {
        IClock clock = new SystemClock();
        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task SsrfGuard_allows_public_https_and_blocks_loopback()
    {
        await SsrfGuard.EnsurePublicEndpointAsync(new Uri("https://example.com/hook"));
        var act = async () => await SsrfGuard.EnsurePublicEndpointAsync(new Uri("http://127.0.0.1/x"));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
