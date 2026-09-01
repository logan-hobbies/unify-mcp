namespace UnifyMcp.Unifi.Api;

public sealed record UniFiLoginRequest(string Username, string Password, bool Remember = true);
