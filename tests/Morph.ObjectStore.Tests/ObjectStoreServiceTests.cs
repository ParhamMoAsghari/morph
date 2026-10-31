using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Files.Storage;
using Morph.ObjectStore.DependencyInjection;
using Morph.ObjectStore.Ports;
using Morph.ObjectStore.Services;
using Xunit;

namespace Morph.ObjectStore.Tests;

public sealed class ObjectStoreServiceTests
{
    [Fact]
    public async Task Put_get_delete_round_trip_via_in_memory_storage()
    {
        var storage = new InMemoryObjectStorage();
        var store = new ObjectStoreService(storage);

        await store.EnsureBucketAsync("demo");
        var put = await store.PutAsync("demo", "a.txt", "text/plain", TimeSpan.FromMinutes(5));
        put.Should().NotBeNull();
        put.IsAbsoluteUri.Should().BeTrue();

        var get = await store.GetAsync("demo", "a.txt", TimeSpan.FromMinutes(5));
        get.ToString().Should().Contain("verb=GET");

        var signed = await store.GetSignedUrlAsync("demo", "a.txt", TimeSpan.FromMinutes(1));
        signed.ToString().Should().Contain("ttl=60");

        await store.DeleteAsync("demo", "a.txt");
    }

    [Fact]
    public void DI_registers_scoped_object_store()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new InMemoryObjectStorage());
        services.AddSingleton<Morph.Files.Ports.IObjectStorage>(sp => sp.GetRequiredService<InMemoryObjectStorage>());
        services.AddMorphObjectStore();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IObjectStore>();
        store.Should().BeOfType<ObjectStoreService>();
    }
}
