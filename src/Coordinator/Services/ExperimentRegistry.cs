using Contracts;
using Coordinator.Data;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Services;

public sealed class ExperimentRegistry(
    IDbContextFactory<CoordinatorDbContext> dbContextFactory)
{
    public ExperimentResponse Create(
        string name,
        bool simulateFailure = false)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var experiment = new ExperimentEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            SimulateFailure = simulateFailure,
            Status = ExperimentStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AssignedWorkerId = null,
            FinishedAtUtc = null,
            ResultMessage = null,
            Attempt = 0
        };

        dbContext.Experiments.Add(experiment);
        dbContext.SaveChanges();

        return ToResponse(experiment);
    }

    public IReadOnlyCollection<ExperimentResponse> GetAll()
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        return dbContext.Experiments
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .Select(ToResponse)
            .ToArray();
    }

    public ExperimentResponse? GetById(Guid id)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var experiment = dbContext.Experiments
            .AsNoTracking()
            .SingleOrDefault(experiment =>
                experiment.Id == id);

        return experiment is null
            ? null
            : ToResponse(experiment);
    }

    public bool TryAssign(
        Guid id,
        string workerId,
        out ExperimentResponse? assignedExperiment)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var updatedRows = dbContext.Experiments
            .Where(experiment =>
                experiment.Id == id &&
                experiment.Status == ExperimentStatus.Pending)
            .ExecuteUpdate(setters => setters
                .SetProperty(
                    experiment => experiment.Status,
                    ExperimentStatus.Running)
                .SetProperty(
                    experiment => experiment.AssignedWorkerId,
                    workerId)
                .SetProperty(
                    experiment => experiment.FinishedAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(
                    experiment => experiment.ResultMessage,
                    (string?)null)
                .SetProperty(
                    experiment => experiment.Attempt,
                    experiment => experiment.Attempt + 1));

        assignedExperiment = GetById(id);

        return updatedRows == 1;
    }

    public ExperimentResponse? GetNextAssignedToWorker(
        string workerId)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var experiment = dbContext.Experiments
            .AsNoTracking()
            .Where(experiment =>
                experiment.AssignedWorkerId == workerId &&
                experiment.Status == ExperimentStatus.Running)
            .AsEnumerable()
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .FirstOrDefault();

        return experiment is null
            ? null
            : ToResponse(experiment);
    }

    public bool TryComplete(
        Guid id,
        string workerId,
        int attempt,
        bool succeeded,
        string? resultMessage,
        out ExperimentResponse? finishedExperiment)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var finalStatus = succeeded
            ? ExperimentStatus.Completed
            : ExperimentStatus.Failed;

        var finishedAtUtc = DateTimeOffset.UtcNow;

        var updatedRows = dbContext.Experiments
            .Where(experiment =>
                experiment.Id == id &&
                experiment.Status == ExperimentStatus.Running &&
                experiment.AssignedWorkerId == workerId &&
                experiment.Attempt == attempt)
            .ExecuteUpdate(setters => setters
                .SetProperty(
                    experiment => experiment.Status,
                    finalStatus)
                .SetProperty(
                    experiment => experiment.FinishedAtUtc,
                    finishedAtUtc)
                .SetProperty(
                    experiment => experiment.ResultMessage,
                    resultMessage));

        finishedExperiment = GetById(id);

        return updatedRows == 1;
    }

    public ExperimentResponse? GetNextPending()
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var experiment = dbContext.Experiments
            .AsNoTracking()
            .Where(experiment =>
                experiment.Status == ExperimentStatus.Pending)
            .AsEnumerable()
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .FirstOrDefault();

        return experiment is null
            ? null
            : ToResponse(experiment);
    }

    public bool HasRunningExperiment(string workerId)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        return dbContext.Experiments.Any(experiment =>
            experiment.Status == ExperimentStatus.Running &&
            experiment.AssignedWorkerId == workerId);
    }

    public IReadOnlyCollection<ExperimentResponse> GetRunning()
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        return dbContext.Experiments
            .AsNoTracking()
            .Where(experiment =>
                experiment.Status == ExperimentStatus.Running)
            .AsEnumerable()
            .OrderBy(experiment => experiment.CreatedAtUtc)
            .Select(ToResponse)
            .ToArray();
    }

    public bool TryRequeue(
        Guid id,
        string workerId,
        out ExperimentResponse? requeuedExperiment)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var updatedRows = dbContext.Experiments
            .Where(experiment =>
                experiment.Id == id &&
                experiment.Status == ExperimentStatus.Running &&
                experiment.AssignedWorkerId == workerId)
            .ExecuteUpdate(setters => setters
                .SetProperty(
                    experiment => experiment.Status,
                    ExperimentStatus.Pending)
                .SetProperty(
                    experiment => experiment.AssignedWorkerId,
                    (string?)null)
                .SetProperty(
                    experiment => experiment.FinishedAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(
                    experiment => experiment.ResultMessage,
                    (string?)null));

        requeuedExperiment = GetById(id);

        return updatedRows == 1;
    }

    private static ExperimentResponse ToResponse(
        ExperimentEntity experiment)
    {
        return new ExperimentResponse
        {
            Id = experiment.Id,
            Name = experiment.Name,
            Status = experiment.Status,
            CreatedAtUtc = experiment.CreatedAtUtc,
            AssignedWorkerId = experiment.AssignedWorkerId,
            FinishedAtUtc = experiment.FinishedAtUtc,
            ResultMessage = experiment.ResultMessage,
            SimulateFailure = experiment.SimulateFailure,
            Attempt = experiment.Attempt
        };
    }

    public int RequeueInterruptedExperiments()
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        return dbContext.Experiments
            .Where(experiment =>
                experiment.Status == ExperimentStatus.Running)
            .ExecuteUpdate(setters => setters
                .SetProperty(
                    experiment => experiment.Status,
                    ExperimentStatus.Pending)
                .SetProperty(
                    experiment => experiment.AssignedWorkerId,
                    (string?)null)
                .SetProperty(
                    experiment => experiment.FinishedAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(
                    experiment => experiment.ResultMessage,
                    (string?)null));
    }
    
}