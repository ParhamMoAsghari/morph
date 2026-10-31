using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Morph.BuildingBlocks.Results;
using Morph.Sms.Ports;

namespace Morph.Platform.Api.Sms;

public static class SmsEndpoints
{
    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/sms")
            .WithTags("Sms")
            .RequireAuthorization();

        group.MapPost("/send", SendAsync);
        return endpoints;
    }

    private static async Task<IResult> SendAsync(
        SendSmsRequest request,
        ISmsService sms,
        CancellationToken cancellationToken)
    {
        var to = request.To;
        var body = request.Body ?? request.Message;
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(body))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["Both 'to' and message body are required."]
            });
        }

        var result = await sms
            .SendAsync(to, body, request.From, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            var error = result.Error!;
            return error.Code switch
            {
                "validation" => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = [error.Message]
                }),
                _ => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status502BadGateway)
            };
        }

        return Results.Ok(new
        {
            succeeded = result.Value.Succeeded,
            providerMessageId = result.Value.ProviderMessageId
        });
    }
}
