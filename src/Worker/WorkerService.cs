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