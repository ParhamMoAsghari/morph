using FluentAssertions;
using Morph.Mail.Options;
using Morph.Mail.Templates;
using Xunit;

namespace Morph.Mail.Tests;

public sealed class SimpleEmailTemplateRendererTests
{
    [Fact]
    public async Task Render_verify_email_substitutes_model_tokens()
    {
        var sut = new SimpleEmailTemplateRenderer();
        var (subject, html, text) = await sut.RenderAsync(
            "verify-email",
            new Dictionary<string, object?>
            {
                ["display_name"] = "Ada",
                ["verify_url"] = "https://app.test/verify/abc"
            });

        subject.Should().Contain("Verify");
        html.Should().Contain("Ada");
        html.Should().Contain("https://app.test/verify/abc");
        text.Should().Contain("Ada");
        text.Should().Contain("https://app.test/verify/abc");
    }

    [Fact]
    public async Task Render_unknown_template_throws()
    {
        var sut = new SimpleEmailTemplateRenderer();
        var act = async () => await sut.RenderAsync("missing-template", new Dictionary<string, object?>());
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown email template*");
    }

    [Fact]
    public void MailSenderOptions_has_expected_defaults()
    {
        var options = new MailSenderOptions();
        options.SmtpHost.Should().Be("localhost");
        options.SmtpPort.Should().Be(1025);
        options.From.Should().Be("noreply@morph.local");
        options.PublicBaseUrl.Should().Be("http://localhost:5100");
        MailSenderOptions.SectionName.Should().Be("Morph:Mail");
    }
}
