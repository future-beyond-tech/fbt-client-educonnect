using EduConnect.Api.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Features.Push.GetVapidPublicKey;

/// <summary>
/// Returns the VAPID public key so the web client can subscribe with it.
/// Anonymous by design — the public key is, by name, public.
/// </summary>
public static class GetVapidPublicKeyEndpoint
{
    public static IResult Handle(IOptions<WebPushOptions> options)
    {
        var publicKey = options.Value.PublicKey;
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            return Results.Ok(new { publicKey = (string?)null, enabled = false });
        }

        return Results.Ok(new { publicKey, enabled = options.Value.Enabled });
    }
}
