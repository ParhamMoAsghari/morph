using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Morph.Crypto.Options;
using Morph.Crypto.Services;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Morph.Crypto.Tests;

public sealed class MorphCryptoServiceTests
{
    private static MorphCryptoService CreateSut(byte[]? masterKey = null)
    {
        var key = masterKey ?? RandomNumberGenerator.GetBytes(32);
        var options = MsOptions.Create(new MorphCryptoOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(key)
        });
        return new MorphCryptoService(options);
    }

    [Fact]
    public void Encrypt_decrypt_bytes_round_trip()
    {
        var sut = CreateSut();
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var plain = Encoding.UTF8.GetBytes("secret payload");

        var cipher = sut.Encrypt(projectId, plain);
        cipher.Should().NotEqual(plain);

        var roundTrip = sut.Decrypt(projectId, cipher);
        roundTrip.Should().Equal(plain);
    }

    [Fact]
    public void Encrypt_decrypt_string_round_trip()
    {
        var sut = CreateSut();
        var projectId = Guid.NewGuid();

        var cipher = sut.Encrypt(projectId, "hello morph");
        cipher.Should().NotBe("hello morph");

        sut.Decrypt(projectId, cipher).Should().Be("hello morph");
    }

    [Fact]
    public void EncryptField_decrypt_field_round_trip()
    {
        var sut = CreateSut();
        var projectId = Guid.NewGuid();

        var marker = sut.EncryptField(projectId, "ssn-value");
        marker.Should().StartWith(MorphCryptoService.FieldMarkerPrefix);

        sut.DecryptField(projectId, marker).Should().Be("ssn-value");
    }

    [Fact]
    public void DecryptField_passes_through_plain_values()
    {
        var sut = CreateSut();
        sut.DecryptField(Guid.NewGuid(), "already-plain").Should().Be("already-plain");
        sut.DecryptField(Guid.NewGuid(), null).Should().BeNull();
    }

    [Fact]
    public void Decrypt_rejects_other_project_key()
    {
        var sut = CreateSut();
        var cipher = sut.Encrypt(Guid.NewGuid(), "x"u8.ToArray());

        var act = () => sut.Decrypt(Guid.NewGuid(), cipher);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Different_projects_produce_different_ciphertext()
    {
        var sut = CreateSut();
        var plain = "same"u8.ToArray();

        var a = sut.Encrypt(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), plain);
        var b = sut.Encrypt(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), plain);

        // Nonces differ; also keys differ — ciphertext must not match.
        a.Should().NotEqual(b);
    }

    [Fact]
    public void Development_stub_master_key_allows_round_trip()
    {
        var sut = new MorphCryptoService(MsOptions.Create(new MorphCryptoOptions()));
        var projectId = Guid.NewGuid();
        var marker = sut.EncryptField(projectId, "dev");
        sut.DecryptField(projectId, marker).Should().Be("dev");
    }
}
