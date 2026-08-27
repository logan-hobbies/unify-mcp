namespace UnifyMcp.Unifi;

public sealed class ReadOnlyViolationException(string message) : InvalidOperationException(message);
