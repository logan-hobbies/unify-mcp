using System.Text.Json;
using Refit;

namespace UnifyMcp.Unifi.Api;

public static class UniFiRefitSettings
{
    // UniFi expects camelCase request bodies (e.g. {"username":...,"password":...}).
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RefitSettings Create() => new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonOptions),
    };
}
