using Contracts;
using Coordinator.Data;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Services;

public sealed class ExperimentRegistry(
    IDbContextFactory<CoordinatorDbContext> dbContextFactory)
{
    public ExperimentResponse Create(
        string name,
        string algorithm,
        string environment,
        int seed,
        int maxSteps,
        int priority,
        bool simulateFailure = false)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        var experiment = new ExperimentEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Algorithm = algorithm,
            Environment = environment,
            Seed = seed,
            MaxSteps = maxSteps,
            Priority = priority,
            SimulateFailure = simulateFailure,
            Status = ExperimentStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AssignedWorkerId = null,
            FinishedAtUtc = null,
            ResultMessage = null,
            MetricsJson = null,
            ExecutionDurationMs = null,
            Attempt = 0
        };

        dbContext.Experiments.Add(experiment);

        var createdEvent = new ExperimentEventEntity
        {
            Id = Guid.NewGuid(),
            ExperimentId = experiment.Id,
            Type = ExperimentEventType.Created,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            WorkerId = null,
            Attempt = 0,
            Details =
                $"Experiment '{experiment.Name}' was created " +
                $"for algorithm '{experiment.Algorithm}' and " +
                $"environment '{experiment.Environment}'."
        };

        dbContext.ExperimentEvents.Add(createdEvent);

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

        using var transaction =
            dbContext.Database.BeginTransaction();

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
                    experiment => experiment.Attempt + 1)
                .SetProperty(
                    experiment => experiment.MetricsJson,
                    (string?)null)
                .SetProperty(
                    experiment => experiment.ExecutionDurationMs,
                    (long?)null));

        var experiment = dbContext.Experiments
            .AsNoTracking()
            .SingleOrDefault(experiment =>
                experiment.Id == id);

        assignedExperiment = experiment is null
            ? null
            : ToResponse(experiment);

        if (updatedRows != 1 || experiment is null)
        {
            return false;
        }

        dbContext.ExperimentEvents.Add(
            CreateEvent(
                experiment.Id,
                ExperimentEventType.Assigned,
                workerId,
                experiment.Attempt,
                $"Experiment was assigned to worker '{workerId}'."));

        dbContext.SaveChanges();
        transaction.Commit();

        return true;

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
        out ExperimentResponse? finishedExperiment,
        string? metricsJson,
        long? executionDurationMs)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        using var transaction =
            dbContext.Database.BeginTransaction();

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
                    resultMessage)
                .SetProperty(
                    experiment => experiment.MetricsJson,
                    metricsJson)
                .SetProperty(
                    experiment => experiment.ExecutionDurationMs,
                    executionDurationMs));

        var experiment = dbContext.Experiments
            .AsNoTracking()
            .SingleOrDefault(experiment =>
                experiment.Id == id);

        finishedExperiment = experiment is null
            ? null
            : ToResponse(experiment);

        if (updatedRows != 1 || experiment is null)
        {
            return false;
        }

        var eventType = succeeded
            ? ExperimentEventType.Completed
            : ExperimentEventType.Failed;

        var details = succeeded
            ? "Experiment completed successfully."
            : $"Experiment failed. {resultMessage}";

        dbContext.ExperimentEvents.Add(
            CreateEvent(
                experiment.Id,
                eventType,
                workerId,
                attempt,
                details));

        dbContext.SaveChanges();
        transaction.Commit();

        return true;
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
            .OrderByDescending(experiment =>
                experiment.Priority)
            .ThenBy(experiment =>
                experiment.CreatedAtUtc)
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

        using var transaction =
            dbContext.Database.BeginTransaction();

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
                    (string?)null)
                .SetProperty(
                    experiment => experiment.MetricsJson,
                    (string?)null)
                .SetProperty(
                    experiment => experiment.ExecutionDurationMs,
                    (long?)null));    
            
        var experiment = dbContext.Experiments
            .AsNoTracking()
            .SingleOrDefault(experiment =>
                experiment.Id == id);

        requeuedExperiment = experiment is null
            ? null
            : ToResponse(experiment);

        if (updatedRows != 1 || experiment is null)
        {
            return false;
        }

        dbContext.ExperimentEvents.Add(
            CreateEvent(
                experiment.Id,
                ExperimentEventType.Requeued,
                workerId,
                experiment.Attempt,
                $"Experiment was returned to Pending because worker '{workerId}' became unavailable."));

        dbContext.SaveChanges();
        transaction.Commit();

        return true;

    }

    private static ExperimentEventEntity CreateEvent(
        Guid experimentId,
        ExperimentEventType type,
        string? workerId,
        int attempt,
        string details)
    {
        return new ExperimentEventEntity
        {
            Id = Guid.NewGuid(),
            ExperimentId = experimentId,
            Type = type,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            WorkerId = workerId,
            Attempt = attempt,
            Details = details
        };
    }

    private static ExperimentResponse ToResponse(
        ExperimentEntity experiment)
    {
        return new ExperimentResponse
        {
            Id = experiment.Id,
            Name = experiment.Name,
            Status = experiment.Status,
            Algorithm = experiment.Algorithm,
            Environment = experiment.Environment,
            Seed = experiment.Seed,
            MaxSteps = experiment.MaxSteps,
            Priority = experiment.Priority,
            CreatedAtUtc = experiment.CreatedAtUtc,
            AssignedWorkerId = experiment.AssignedWorkerId,
            FinishedAtUtc = experiment.FinishedAtUtc,
            ResultMessage = experiment.ResultMessage,
            SimulateFailure = experiment.SimulateFailure,
            Attempt = experiment.Attempt,
            MetricsJson = experiment.MetricsJson,
            ExecutionDurationMs = experiment.ExecutionDurationMs
        };
    }

    public int RequeueInterruptedExperiments()
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        using var transaction =
            dbContext.Database.BeginTransaction();

        var interruptedExperiments = dbContext.Experiments
            .Where(experiment =>
                experiment.Status == ExperimentStatus.Running)
            .ToArray();

        foreach (var experiment in interruptedExperiments)
        {
            var previousWorkerId = experiment.AssignedWorkerId;

            experiment.Status = ExperimentStatus.Pending;
            experiment.AssignedWorkerId = null;
            experiment.FinishedAtUtc = null;
            experiment.ResultMessage = null;

            dbContext.ExperimentEvents.Add(
                CreateEvent(
                    experiment.Id,
                    ExperimentEventType.RecoveredOnStartup,
                    previousWorkerId,
                    experiment.Attempt,
                    "Experiment was returned to Pending after Coordinator restart."));
        }

        dbContext.SaveChanges();
        transaction.Commit();

        return interruptedExperiments.Length;
    }

    public IReadOnlyCollection<ExperimentEventResponse> GetEvents(
    Guid experimentId)
    {
        using var dbContext =
            dbContextFactory.CreateDbContext();

        return dbContext.ExperimentEvents
            .AsNoTracking()
            .Where(experimentEvent =>
                experimentEvent.ExperimentId == experimentId)
            .AsEnumerable()
            .OrderBy(experimentEvent =>
                experimentEvent.OccurredAtUtc)
            .Select(experimentEvent =>
                new ExperimentEventResponse
                {
                    Id = experimentEvent.Id,
                    Type = experimentEvent.Type.ToString(),
                    OccurredAtUtc = experimentEvent.OccurredAtUtc,
                    WorkerId = experimentEvent.WorkerId,
                    Attempt = experimentEvent.Attempt,
                    Details = experimentEvent.Details
                })
            .ToArray();
    }
    
}