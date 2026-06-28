using Contracts;

namespace Worker.Execution;

public sealed class SimulatedExperimentExecutor(
    IConfiguration configuration) : IExperimentExecutor
{
    private readonly TimeSpan _executionDuration =
        TimeSpan.FromSeconds(
            ReadPositiveInt(
                configuration,
                "Worker:SimulatedExecutionSeconds",
                3));

    public async Task<ExperimentExecutionResult> ExecuteAsync(
        ExperimentResponse experiment,
        CancellationToken cancellationToken)
    {
        await Task.Delay(
            _executionDuration,
            cancellationToken);

        return experiment.SimulateFailure
            ? new ExperimentExecutionResult(
                false,
                "Simulated execution failure.")
            : new ExperimentExecutionResult(
                true,
                "Simulated execution completed successfully.");
    }

    private static int ReadPositiveInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        return int.TryParse(configuration[key], out var value) &&
               value > 0
            ? value
            : defaultValue;
    }
}