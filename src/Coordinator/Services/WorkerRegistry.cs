using System.Collections.Concurrent;
using Contracts;

namespace Coordinator.Services;

public sealed class WorkerRegistry
{
    private readonly ConcurrentDictionary<string, WorkerStatusResponse> _workers = new();

    public WorkerStatusResponse Register(string workerId)
    {
        var worker = new WorkerStatusResponse
        {
            WorkerId = workerId,
            RegisteredAtUtc = DateTimeOffset.UtcNow
        };

        _workers[workerId] = worker;

        return worker;
    }

    public IReadOnlyCollection<WorkerStatusResponse> GetAll()
    {
        return _workers.Values
            .OrderBy(worker => worker.WorkerId)
            .ToArray();
    }
}