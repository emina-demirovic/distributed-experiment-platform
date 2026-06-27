namespace Contracts;

public sealed class CompleteExperimentRequest
{
    public string WorkerId { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public string? ResultMessage { get; set; }

    public int Attempt { get; set; }
    
}