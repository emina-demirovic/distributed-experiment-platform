namespace Coordinator.Services;

public sealed class ExperimentRecoveryService(
    ILogger<ExperimentRecoveryService> logger,
    WorkerRegistry workerRegistry,
    ExperimentRegistry experimentRegistry) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var runningExperiments =
                experimentRegistry.GetRunning();

            foreach (var experiment in runningExperiments)
            {
                var workerId = experiment.AssignedWorkerId;

                if (string.IsNullOrWhiteSpace(workerId))
                {
                    continue;
                }

                if (workerRegistry.IsOnline(workerId))
                {
                    continue;
                }

                var requeued = experimentRegistry.TryRequeue(
                    experiment.Id,
                    workerId,
                    out var requeuedExperiment);

                if (requeued && requeuedExperiment is not null)
                {
                    logger.LogWarning(
                        "Experiment {ExperimentId} returned to Pending because worker {WorkerId} is offline.",
                        experiment.Id,
                        workerId);
                }
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}