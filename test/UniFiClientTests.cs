using System.Net;
using System.Text.Json;
using Refit;
using UnifyMcp.Unifi;
using UnifyMcp.Unifi.Api;

namespace UnifyMcp.Tests;

public class UniFiClientTests
{
    [Fact]
    public void Integration_interface_is_get_only()
    {
        var methods = typeof(IUniFiIntegrationApi).GetMethods();
        Assert.All(methods, method =>
        {
            Assert.Contains(method.GetCustomAttributes(false), attr => attr is GetAttribute);
            Assert.DoesNotContain(method.GetCustomAttributes(false), attr =>
                attr is PostAttribute or PutAttribute or PatchAttribute or DeleteAttribute);
        });
    }

    [Fact]
    public void Classic_interface_is_get_only()
    {
        var methods = typeof(IUniFiClassicApi).GetMethods();
        Assert.All(methods, method =>
        {
            Assert.Contains(method.GetCustomAttributes(false), attr => attr is GetAttribute);
            Assert.DoesNotContain(method.GetCustomAttributes(false), attr =>
                attr is PostAttribute or PutAttribute or PatchAttribute or DeleteAttribute);
        });
    }

    [Fact]
    public async Task Refit_builds_integration_info_url()
    {
        var handler = new RecordingHandler("""{"applicationVersion":"10.0.0"}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unifi.local/") };
        var api = RestService.For<IUniFiIntegrationApi>(http, UniFiRefitSettings.Create());

        var result = await api.GetInfoAsync();

        Assert.Equal("https://unifi.local/proxy/network/integration/v1/info", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("10.0.0", result.GetProperty("applicationVersion").GetString());
    }

    [Fact]
    public async Task Refit_builds_classic_anomalies_url()
    {
        var handler = new RecordingHandler("""{"data":[]}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unifi.local/") };
        var api = RestService.For<IUniFiClassicApi>(http, UniFiRefitSettings.Create());

        await api.GetAnomaliesAsync("default", within: 24);

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.StartsWith(
            "https://unifi.local/proxy/network/api/s/default/stat/anomalies",
            handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("within=24", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public void SummarizeHealth_maps_subsystems()
    {
        using var document = JsonDocument.Parse("""
            {
              "data": [
                {
                  "subsystem": "wan",
                  "status": "ok",
                  "latency": 12,
                  "tx_bytes": 100,
                  "rx_bytes": 200
                }
              ]
            }
            """);

        var summary = UniFiDiagnostics.SummarizeHealth(document.RootElement);
        Assert.Contains("wan", summary.GetRawText());
        Assert.Contains("ok", summary.GetRawText());
    }

    [Fact]
    public void FilterEvents_matches_keywords()
    {
        using var document = JsonDocument.Parse("""
            [
              { "msg": "User disconnected", "key": "evt1" },
              { "msg": "AP adopted", "key": "evt2" },
              { "msg": "Client blocked by firewall", "key": "evt3" }
            ]
            """);

        var events = document.RootElement.EnumerateArray().ToList();
        var matches = UniFiDiagnostics.FilterEvents(events, ["disconnect", "blocked"], 10);
        Assert.Equal(2, matches.Count);
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
