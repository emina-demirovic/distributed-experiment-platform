using System.Net.Http.Json;
using Contracts;

namespace Worker;

public sealed class WorkerService(
    ILogger<WorkerService> logger,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient();

        var request = new WorkerRegistrationRequest
        {
            WorkerId = "worker-1"
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                "http://localhost:5031/api/workers/register",
                request,
                stoppingToken);

            response.EnsureSuccessStatusCode();

            logger.LogInformation(
                "Worker {WorkerId} successfully registered.",
                request.WorkerId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Worker registration failed.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(10),
                stoppingToken);
        }
    }
}