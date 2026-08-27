using System.Text.Json;
using UnifyMcp.Unifi;

namespace UnifyMcp.Tests;

public class UniFiClientTests
{
    [Fact]
    public void AssertGetOnly_blocks_post()
    {
        var ex = Assert.ThrowsAny<Exception>(() => InvokeAssertGetOnly("POST"));
        var violation = ex as ReadOnlyViolationException ?? ex.InnerException as ReadOnlyViolationException;
        Assert.NotNull(violation);
        Assert.Contains("POST", violation!.Message);
    }

    [Fact]
    public void AssertReadOnlyPath_blocks_actions()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            InvokeAssertReadOnlyPath("/v1/sites/abc/devices/123/actions"));
        var violation = ex as ReadOnlyViolationException ?? ex.InnerException as ReadOnlyViolationException;
        Assert.NotNull(violation);
        Assert.Contains("actions", violation!.Message);
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

        var summary = UniFiClient.SummarizeHealth(document.RootElement);
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
        var matches = UniFiClient.FilterEvents(events, ["disconnect", "blocked"], 10);
        Assert.Equal(2, matches.Count);
    }

    private static void InvokeAssertGetOnly(string method)
    {
        var methodInfo = typeof(UniFiClient).GetMethod(
            "AssertGetOnly",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        methodInfo!.Invoke(null, [method]);
    }

    private static void InvokeAssertReadOnlyPath(string path)
    {
        var methodInfo = typeof(UniFiClient).GetMethod(
            "AssertReadOnlyPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        methodInfo!.Invoke(null, [path]);
    }
}
