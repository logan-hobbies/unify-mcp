using System.Reflection;
using ModelContextProtocol.AspNetCore;
using UnifyMcp.Auth;
using UnifyMcp.Configuration;
using UnifyMcp.Secrets;
using UnifyMcp.Tools;
using UnifyMcp.Unifi;

var mcpSettings = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build()
    .GetSection("Mcp").Get<McpSettings>() ?? new McpSettings();

var transport = mcpSettings.Transport.Trim().ToLowerInvariant();
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

if (transport is "stdio")
{
    // Stdio: plain generic host, no Kestrel.
    var host = Host.CreateApplicationBuilder(args);
    RegisterCore(host.Services, host.Configuration);
    host.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<UniFiTools>();
    host.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    await host.Build().RunAsync();
    return;
}

if (string.IsNullOrWhiteSpace(mcpSettings.AuthToken) && !mcpSettings.AllowAnonymous)
{
    throw new InvalidOperationException(
        "Mcp:AuthToken is required for HTTP transport. Set Mcp__AuthToken, or set " +
        "Mcp__AllowAnonymous=true for local development only.");
}

var builder = WebApplication.CreateBuilder(args);
RegisterCore(builder.Services, builder.Configuration);
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<UniFiTools>();

// Bind from Mcp settings only; ignore launchSettings so the listen address is deliberate.
builder.WebHost.UseUrls($"http://{mcpSettings.Host}:{mcpSettings.Port}");

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

        if (!BearerTokenAuth.Matches(context.Request.Headers.Authorization.ToString(), expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        await next();
    });
}

app.MapGet("/health", () => Results.Json(new { status = "ok", service = "unify-mcp", version }));
app.MapMcp("/mcp");

app.Lifetime.ApplicationStopping.Register(() =>
{
    var client = app.Services.GetService<UniFiClient>();
    client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
});

await app.RunAsync();

static void RegisterCore(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<AzureKeyVaultSettings>(configuration.GetSection("AzureKeyVault"));
    services.Configure<UnifiSettings>(configuration.GetSection("Unifi"));
    services.Configure<McpSettings>(configuration.GetSection("Mcp"));

    services.AddSingleton<AzureSecretStore>();
    services.AddSingleton<UniFiClient>();
    services.AddSingleton<UniFiService>();
}
