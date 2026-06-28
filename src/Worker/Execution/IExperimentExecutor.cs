using Contracts;

namespace Worker.Execution;

public interface IExperimentExecutor
{
    Task<ExperimentExecutionResult> ExecuteAsync(
        ExperimentResponse experiment,
        CancellationToken cancellationToken);
}