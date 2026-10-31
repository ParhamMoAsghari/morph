using Xunit;

namespace Morph.Cli.Tests;

/// <summary>Serializes tests that change <see cref="Directory.GetCurrentDirectory"/>.</summary>
[CollectionDefinition("CliWorkingDirectory", DisableParallelization = true)]
public sealed class CliWorkingDirectoryCollection;
