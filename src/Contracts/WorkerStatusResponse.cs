namespace Contracts;

public sealed class WorkerStatusResponse
{
    public string WorkerId { get; set; } = string.Empty;

    public DateTimeOffset RegisteredAtUtc { get; set; }
}