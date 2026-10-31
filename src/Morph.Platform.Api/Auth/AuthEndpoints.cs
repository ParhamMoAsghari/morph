using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Morph.BuildingBlocks.Time;
using Morph.Identity.Auth;
using Morph.Identity.Users;
using Morph.Mail.Options;
using Morph.Mail.Services;

namespace Morph.Platform.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/verify-email", VerifyEmailAsync)
            .WithName("VerifyEmail")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/me", GetMeAsync)
            .RequireAuthorization()
            .WithName("GetMe")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<PlatformUser> userManager,
        IClock clock,
        MailService mail,
        IOptions<MailSenderOptions> mailOptions,
        CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Results.Problem(
                detail: "A user with this email already exists.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                type: "https://httpstatuses.com/409");
        }

        var user = new PlatformUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false,
            DisplayName = request.DisplayName,
            CreatedAt = clock.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        // Best-effort verification email via outbox; auth still works (RequireConfirmedAccount=false).
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = mailOptions.Value.PublicBaseUrl.TrimEnd('/');
            var verifyUrl =
                $"{baseUrl}/v1/auth/verify-email?userId={user.Id:N}&token={Uri.EscapeDataString(encoded)}";

            await mail.EnqueueTemplateAsync(
                    user.Email!,
                    "verify-email",
                    new Dictionary<string, object?>
                    {
                        ["display_name"] = user.DisplayName ?? user.Email,
                        ["verify_url"] = verifyUrl
                    },
                    correlationId: user.Id.ToString("N"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Do not fail registration if mail/outbox is unavailable.
        }

        var response = new RegisterResponse(user.Id, user.Email!, user.DisplayName);
        return Results.Created($"/v1/auth/me", response);
    }

    private static async Task<IResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        UserManager<PlatformUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["User not found."]
            });
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["token"] = ["Invalid token encoding."]
            });
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        return Results.Ok(new { verified = true });
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<PlatformUser> userManager,
        MailService mail,
        IOptions<MailSenderOptions> mailOptions,
        CancellationToken cancellationToken)
    {
        // Always accept to avoid account enumeration.
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            try
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var baseUrl = mailOptions.Value.PublicBaseUrl.TrimEnd('/');
                var resetUrl =
                    $"{baseUrl}/v1/auth/reset-password?userId={user.Id:N}&token={Uri.EscapeDataString(encoded)}";

                await mail.EnqueueTemplateAsync(
                        user.Email!,
                        "reset-password",
                        new Dictionary<string, object?>
                        {
                            ["display_name"] = user.DisplayName ?? user.Email,
                            ["reset_url"] = resetUrl
                        },
                        correlationId: user.Id.ToString("N"),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // swallow — still return 202
            }
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<PlatformUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["User not found."]
            });
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["token"] = ["Invalid token encoding."]
            });
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        return Results.Ok(new { reset = true });
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        UserManager<PlatformUser> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            var subject = httpContext.User.FindFirst("sub")?.Value
                ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(subject, out var userId))
            {
                user = await userManager.FindByIdAsync(userId.ToString());
            }
        }

        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            user.Id,
            user.Email,
            user.EmailConfirmed,
            user.DisplayName,
            user.CreatedAt,
            user.LastLoginAt,
            PrincipalType = AuthConstants.PrincipalTypePlatform
        });
    }
}

public sealed record VerifyEmailRequest(Guid UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
