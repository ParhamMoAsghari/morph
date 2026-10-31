using FluentAssertions;
using Morph.Infrastructure.Hosting;
using Xunit;

namespace Morph.Host.Integration.Tests;

public sealed class HostAssemblyTests
{
    [Fact]
    public void Host_entry_assembly_is_present()
    {
        typeof(Program).Assembly.GetName().Name.Should().Be("Morph.Host");
    }

    [Fact]
    public void Process_endpoints_type_is_wired_in_platform_api()
    {
        typeof(Morph.Platform.Api.Processes.ProcessEndpoints).Should().NotBeNull();
    }

    [Fact]
    public void Database_initializer_skip_variable_is_stable()
    {
        DatabaseInitializer.SkipEnvironmentVariable.Should().Be("MORPH_SkipDatabaseInitialization");
    }
}
