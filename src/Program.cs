using ModelContextProtocol.AspNetCore;
using UnifyMcp.Configuration;
using UnifyMcp.Secrets;
using UnifyMcp.Tools;
using UnifyMcp.Unifi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureKeyVaultSettings>(builder.Configuration.GetSection("AzureKeyVault"));
builder.Services.Configure<UnifiSettings>(builder.Configuration.GetSection("Unifi"));
builder.Services.Configure<McpSettings>(builder.Configuration.GetSection("Mcp"));

builder.Services.AddSingleton<AzureSecretStore>();
builder.Services.AddSingleton<UniFiClient>();
builder.Services.AddSingleton<UniFiService>();

var mcpSettings = builder.Configuration.GetSection("Mcp").Get<McpSettings>() ?? new McpSettings();
var transport = mcpSettings.Transport.Trim().ToLowerInvariant();

if (transport is "stdio")
{
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<UniFiTools>();
}
else
{
    builder.Services.AddMcpServer()
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithTools<UniFiTools>();
}

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(mcpSettings.AuthToken))
{
    var expected = mcpSettings.AuthToken;
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next();
            return;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization != $"Bearer {expected}")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next();
    });
}

app.MapGet("/health", () => Results.Json(new { status = "ok", service = "unify-mcp", version = "0.2.0" }));

if (transport is not "stdio")
{
    app.MapMcp();
    app.Urls.Add($"http://{mcpSettings.Host}:{mcpSettings.Port}");
}

app.Lifetime.ApplicationStopping.Register(() =>
{
    var client = app.Services.GetService<UniFiClient>();
    client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
});

await app.RunAsync();
