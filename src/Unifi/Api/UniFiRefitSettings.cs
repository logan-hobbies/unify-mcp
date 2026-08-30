using System.Text.Json;
using Refit;

namespace UnifyMcp.Unifi.Api;

public static class UniFiRefitSettings
{
    public static RefitSettings Create() => new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }),
    };
}
