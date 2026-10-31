using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Morph.Crypto.Options;
using Morph.Crypto.Services;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Morph.Crypto.Tests;

public sealed class MorphCryptoServiceEdgeTests
{
    [Fact]
    public void Encrypt_decrypt_empty_plaintext_round_trips()
    {
        var sut = new MorphCryptoService(MsOptions.Create(new MorphCryptoOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));
        var projectId = Guid.NewGuid();

        var cipher = sut.Encrypt(projectId, ReadOnlySpan<byte>.Empty);
        cipher.Should().NotBeEmpty();

        sut.Decrypt(projectId, cipher).Should().BeEmpty();
        sut.Decrypt(projectId, sut.Encrypt(projectId, string.Empty)).Should().Be(string.Empty);
    }

    [Fact]
    public void Different_master_keys_produce_different_ciphertext_for_same_project()
    {
        var projectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var plain = Encoding.UTF8.GetBytes("shared-secret");

        var sutA = new MorphCryptoService(MsOptions.Create(new MorphCryptoOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));
        var sutB = new MorphCryptoService(MsOptions.Create(new MorphCryptoOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));

        var cipherA = sutA.Encrypt(projectId, plain);
        var cipherB = sutB.Encrypt(projectId, plain);

        cipherA.Should().NotEqual(cipherB);

        var act = () => sutB.Decrypt(projectId, cipherA);
        act.Should().Throw<CryptographicException>();
    }
}
