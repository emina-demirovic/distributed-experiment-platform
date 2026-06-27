namespace Contracts;

public sealed class CreateExperimentRequest
{
    public string Name { get; set; } = string.Empty;
    public bool SimulateFailure { get; set; }
}