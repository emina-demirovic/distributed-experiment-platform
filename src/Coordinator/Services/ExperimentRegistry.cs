using System.Collections.Concurrent;
using Contracts;

namespace Coordinator.Services;

public sealed class ExperimentRegistry
{
    private readonly ConcurrentDictionary<Guid, ExperimentResponse> _experiments =
        new();

    public ExperimentResponse Create(string name)
    {
        var experiment = new ExperimentResponse
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = ExperimentStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AssignedWorkerId = null
        };

        _experiments[experiment.Id] = experiment;

        return experiment;
    }

    public IReadOnlyCollection<ExperimentResponse> GetAll()
    {
        return _experiments.Values
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .ToArray();
    }

    public ExperimentResponse? GetById(Guid id)
    {
        return _experiments.GetValueOrDefault(id);
    }
}