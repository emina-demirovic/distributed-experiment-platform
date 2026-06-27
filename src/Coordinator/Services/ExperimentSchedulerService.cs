namespace Coordinator.Services;

public sealed class ExperimentSchedulerService(
    ILogger<ExperimentSchedulerService> logger,
    WorkerRegistry workerRegistry,
    ExperimentRegistry experimentRegistry) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var onlineWorkers = workerRegistry.GetOnlineWorkers();

            foreach (var worker in onlineWorkers)
            {
                if (experimentRegistry.HasRunningExperiment(
                    worker.WorkerId))
                {
                    continue;
                }

                while (true)
                {
                    var pendingExperiment =
                        experimentRegistry.GetNextPending();

                    if (pendingExperiment is null)
                    {
                        break;
                    }

                    var assigned = experimentRegistry.TryAssign(
                        pendingExperiment.Id,
                        worker.WorkerId,
                        out var assignedExperiment);

                    if (!assigned || assignedExperiment is null)
                    {
                        continue;
                    }

                    logger.LogInformation(
                        "Experiment {ExperimentId} automatically assigned to worker {WorkerId}.",
                        assignedExperiment.Id,
                        worker.WorkerId);

                    break;
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