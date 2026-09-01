using UnifyMcp.Secrets;

namespace UnifyMcp.Unifi.Api;

public sealed class UniFiApiKeyHandler(AzureSecretStore secrets) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = await secrets.GetUnifiApiKeyAsync(cancellationToken);
        request.Headers.Remove("X-API-KEY");
        request.Headers.Add("X-API-KEY", apiKey);
        return await base.SendAsync(request, cancellationToken);
    }
}
