using System.Net.Http.Json;
using System.Net;
using Contracts;

namespace Worker;

public sealed class WorkerService(
    ILogger<WorkerService> logger,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private const string WorkerId = "worker-1";

    private const string CoordinatorBaseUrl =
        "http://localhost:5031";

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient();

        var registrationRequest = new WorkerRegistrationRequest
        {
            WorkerId = WorkerId
        };

        try
        {
            var registrationResponse = await client.PostAsJsonAsync(
                $"{CoordinatorBaseUrl}/api/workers/register",
                registrationRequest,
                stoppingToken);

            registrationResponse.EnsureSuccessStatusCode();

            logger.LogInformation(
                "Worker {WorkerId} successfully registered.",
                WorkerId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Worker registration failed.");

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeatResponse = await client.PostAsync(
                    $"{CoordinatorBaseUrl}/api/workers/{WorkerId}/heartbeat",
                    content: null,
                    stoppingToken);

                heartbeatResponse.EnsureSuccessStatusCode();

                logger.LogInformation(
                    "Heartbeat sent by {WorkerId}.",
                    WorkerId);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Heartbeat failed for {WorkerId}.",
                    WorkerId);
            }

            var nextExperimentResponse = await client.GetAsync(
                $"{CoordinatorBaseUrl}/api/experiments/worker/{WorkerId}/next",
                stoppingToken);

            if (nextExperimentResponse.StatusCode != HttpStatusCode.NoContent)
            {
                nextExperimentResponse.EnsureSuccessStatusCode();

                var experiment =
                    await nextExperimentResponse.Content
                        .ReadFromJsonAsync<ExperimentResponse>(
                            cancellationToken: stoppingToken);

                if (experiment is not null)
                {
                    logger.LogInformation(
                        "Starting experiment {ExperimentId}: {ExperimentName}.",
                        experiment.Id,
                        experiment.Name);

                    // Za sada „izvršavanje“ traje tri sekunde. 
                    // Kasnije će taj deo zameniti poziv stvarnog RL procesa ili izvršnog modula.
                    await Task.Delay(
                        TimeSpan.FromSeconds(3),
                        stoppingToken);

                    var completionRequest = new CompleteExperimentRequest
                    {
                        WorkerId = WorkerId,
                        Succeeded = !experiment.SimulateFailure,
                        ResultMessage = experiment.SimulateFailure
                            ? "Simulated execution failure."
                            : "Simulated execution completed successfully."
                    };

                    var completionResponse = await client.PostAsJsonAsync(
                        $"{CoordinatorBaseUrl}/api/experiments/{experiment.Id}/complete",
                        completionRequest,
                        stoppingToken);

                    completionResponse.EnsureSuccessStatusCode();

                    if (experiment.SimulateFailure)
                    {
                        logger.LogWarning(
                            "Experiment {ExperimentId} failed.",
                            experiment.Id);
                    }
                    else
                    {
                        logger.LogInformation(
                            "Experiment {ExperimentId} completed successfully.",
                            experiment.Id);
                    }
                }
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
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