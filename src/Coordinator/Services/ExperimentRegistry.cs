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

    public bool TryAssign(
        Guid id,
        string workerId,
        out ExperimentResponse? assignedExperiment)
    {
        while (true)
        {
            if (!_experiments.TryGetValue(id, out var existingExperiment))
            {
                assignedExperiment = null;
                return false;
            }

            if (existingExperiment.Status != ExperimentStatus.Pending)
            {
                assignedExperiment = existingExperiment;
                return false;
            }

            var updatedExperiment = new ExperimentResponse
            {
                Id = existingExperiment.Id,
                Name = existingExperiment.Name,
                Status = ExperimentStatus.Running,
                CreatedAtUtc = existingExperiment.CreatedAtUtc,
                AssignedWorkerId = workerId
            };

            if (_experiments.TryUpdate(
                id,
                updatedExperiment,
                existingExperiment))
            {
                assignedExperiment = updatedExperiment;
                return true;
            }
        }
    }

    
}