using Refit;

namespace UnifyMcp.Unifi.Api;

public interface IUniFiAuthApi
{
    [Post("/api/auth/login")]
    Task<HttpResponseMessage> LoginAsync([Body] UniFiLoginRequest request, CancellationToken cancellationToken = default);
}
