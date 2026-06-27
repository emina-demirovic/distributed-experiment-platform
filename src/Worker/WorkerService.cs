using System.Net;
using System.Net.Http.Json;
using Contracts;

namespace Worker;

public sealed class WorkerService(
    ILogger<WorkerService> logger,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private const string WorkerId = "worker-1";

    private const string CoordinatorBaseUrl =
        "http://localhost:5031";

    private static readonly TimeSpan HeartbeatInterval =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ExperimentPollingInterval =
        TimeSpan.FromSeconds(2);

    // Privremeno 20 sekundi kako bismo proverili
    // da heartbeat radi i tokom izvršavanja.
    private static readonly TimeSpan SimulatedExecutionDuration =
        TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient();

        var registered = await RegisterAsync(
            client,
            stoppingToken);

        if (!registered)
        {
            return;
        }

        await Task.WhenAll(
            RunHeartbeatLoopAsync(client, stoppingToken),
            RunExecutionLoopAsync(client, stoppingToken));
    }

    private async Task<bool> RegisterAsync(
        HttpClient client,
        CancellationToken stoppingToken)
    {
        var request = new WorkerRegistrationRequest
        {
            WorkerId = WorkerId
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{CoordinatorBaseUrl}/api/workers/register",
                request,
                stoppingToken);

            response.EnsureSuccessStatusCode();

            logger.LogInformation(
                "Worker {WorkerId} successfully registered.",
                WorkerId);

            return true;
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Worker registration failed.");

            return false;
        }
    }

    private async Task RunHeartbeatLoopAsync(
        HttpClient client,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await client.PostAsync(
                    $"{CoordinatorBaseUrl}/api/workers/{WorkerId}/heartbeat",
                    content: null,
                    stoppingToken);

                response.EnsureSuccessStatusCode();

                logger.LogDebug(
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

            if (!await WaitAsync(
                    HeartbeatInterval,
                    stoppingToken))
            {
                break;
            }
        }
    }

    private async Task RunExecutionLoopAsync(
        HttpClient client,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await client.GetAsync(
                    $"{CoordinatorBaseUrl}/api/experiments/worker/{WorkerId}/next",
                    stoppingToken);

                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    if (!await WaitAsync(
                            ExperimentPollingInterval,
                            stoppingToken))
                    {
                        break;
                    }

                    continue;
                }

                response.EnsureSuccessStatusCode();

                var experiment =
                    await response.Content
                        .ReadFromJsonAsync<ExperimentResponse>(
                            cancellationToken: stoppingToken);

                if (experiment is not null)
                {
                    await ExecuteExperimentAsync(
                        client,
                        experiment,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Experiment processing failed.");
            }

            if (!await WaitAsync(
                    ExperimentPollingInterval,
                    stoppingToken))
            {
                break;
            }
        }
    }

    private async Task ExecuteExperimentAsync(
        HttpClient client,
        ExperimentResponse experiment,
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting experiment {ExperimentId}: {ExperimentName}.",
            experiment.Id,
            experiment.Name);

        await Task.Delay(
            SimulatedExecutionDuration,
            stoppingToken);

        var completionRequest = new CompleteExperimentRequest
        {
            WorkerId = WorkerId,
            Succeeded = !experiment.SimulateFailure,
            ResultMessage = experiment.SimulateFailure
                ? "Simulated execution failure."
                : "Simulated execution completed successfully."
        };

        var response = await client.PostAsJsonAsync(
            $"{CoordinatorBaseUrl}/api/experiments/{experiment.Id}/complete",
            completionRequest,
            stoppingToken);

        response.EnsureSuccessStatusCode();

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

    private static async Task<bool> WaitAsync(
        TimeSpan delay,
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}