namespace Contracts;

public sealed class ExperimentResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ExperimentStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? AssignedWorkerId { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string? ResultMessage { get; set; }
}