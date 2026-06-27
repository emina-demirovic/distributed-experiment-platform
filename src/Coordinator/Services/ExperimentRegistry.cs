using System.Collections.Concurrent;
using Contracts;

namespace Coordinator.Services;

public sealed class ExperimentRegistry
{
    private readonly ConcurrentDictionary<Guid, ExperimentResponse> _experiments =
        new();

    public ExperimentResponse Create(string name, bool simulateFailure = false)
    {
        var experiment = new ExperimentResponse
        {
            Id = Guid.NewGuid(),
            Name = name,
            SimulateFailure = simulateFailure,
            Status = ExperimentStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AssignedWorkerId = null,
            FinishedAtUtc = null,
            ResultMessage = null
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
                AssignedWorkerId = workerId,
                FinishedAtUtc = null,
                ResultMessage = null,
                SimulateFailure = existingExperiment.SimulateFailure
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

    public ExperimentResponse? GetNextAssignedToWorker(string workerId)
    {
        return _experiments.Values
            .Where(experiment =>
                experiment.AssignedWorkerId == workerId &&
                experiment.Status == ExperimentStatus.Running)
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .FirstOrDefault();
    }

    public bool TryComplete(
        Guid id,
        string workerId,
        bool succeeded,
        string? resultMessage,
        out ExperimentResponse? finishedExperiment)
    {
        while (true)
        {
            if (!_experiments.TryGetValue(id, out var existingExperiment))
            {
                finishedExperiment = null;
                return false;
            }

            if (existingExperiment.Status != ExperimentStatus.Running ||
                existingExperiment.AssignedWorkerId != workerId)
            {
                finishedExperiment = existingExperiment;
                return false;
            }

            var updatedExperiment = new ExperimentResponse
            {
                Id = existingExperiment.Id,
                Name = existingExperiment.Name,
                Status = succeeded
                    ? ExperimentStatus.Completed
                    : ExperimentStatus.Failed,
                CreatedAtUtc = existingExperiment.CreatedAtUtc,
                AssignedWorkerId = existingExperiment.AssignedWorkerId,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                ResultMessage = resultMessage,
                SimulateFailure = existingExperiment.SimulateFailure
            };

            if (_experiments.TryUpdate(
                id,
                updatedExperiment,
                existingExperiment))
            {
                finishedExperiment = updatedExperiment;
                return true;
            }
        }
    }
    
    public ExperimentResponse? GetNextPending()
    {
        return _experiments.Values
            .Where(experiment =>
                experiment.Status == ExperimentStatus.Pending)
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .FirstOrDefault();
    }

    public bool HasRunningExperiment(string workerId)
    {
        return _experiments.Values.Any(experiment =>
            experiment.Status == ExperimentStatus.Running &&
            experiment.AssignedWorkerId == workerId);
    }
    
}