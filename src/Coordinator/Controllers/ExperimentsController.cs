using Contracts;
using Coordinator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/experiments")]
public sealed class ExperimentsController : ControllerBase
{
    private readonly ExperimentRegistry _experimentRegistry;
    private readonly WorkerRegistry _workerRegistry;


    public ExperimentsController(ExperimentRegistry experimentRegistry, WorkerRegistry workerRegistry)
    {
        _experimentRegistry = experimentRegistry;
        _workerRegistry = workerRegistry;
    }

    [HttpPost]
    public ActionResult<ExperimentResponse> Create(
        CreateExperimentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Experiment name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Algorithm))
        {
            return BadRequest("Algorithm is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Environment))
        {
            return BadRequest("Environment is required.");
        }

        if (request.MaxSteps <= 0)
        {
            return BadRequest("MaxSteps must be greater than zero.");
        }

        if (request.Priority is < 0 or > 10)
        {
            return BadRequest("Priority must be between 0 and 10.");
        }

        if (request.TimeoutSeconds is < 1 or > 86400)
        {
            return BadRequest(
                "TimeoutSeconds must be between 1 and 86400.");
        }

        var experiment = _experimentRegistry.Create(
            request.Name,
            request.Algorithm,
            request.Environment,
            request.Seed,
            request.MaxSteps,
            request.Priority,
            request.TimeoutSeconds,
            request.SimulateFailure);

        return CreatedAtAction(
            nameof(GetById),
            new { id = experiment.Id },
            experiment);
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<ExperimentResponse>> GetAll()
    {
        return Ok(_experimentRegistry.GetAll());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<ExperimentResponse> GetById(Guid id)
    {
        var experiment = _experimentRegistry.GetById(id);

        if (experiment is null)
        {
            return NotFound($"Experiment '{id}' was not found.");
            
        }

        return Ok(experiment);
    }

    [HttpPost("{id:guid}/assign")]
    public ActionResult<ExperimentResponse> Assign(Guid id)
    {
        var existingExperiment = _experimentRegistry.GetById(id);

        if (existingExperiment is null)
        {
            return NotFound($"Experiment '{id}' was not found.");
        }

        if (existingExperiment.Status != ExperimentStatus.Pending)
        {
            return Conflict(
                $"Experiment '{id}' cannot be assigned because its status is " +
                $"'{existingExperiment.Status}'.");
        }

        var worker = _workerRegistry.GetFirstOnline();

        if (worker is null)
        {
            return Conflict("No online worker is currently available.");
        }

        var assigned = _experimentRegistry.TryAssign(
            id,
            worker.WorkerId,
            out var assignedExperiment);

        if (!assigned || assignedExperiment is null)
        {
            return Conflict($"Experiment '{id}' could not be assigned.");
        }

        return Ok(assignedExperiment);
    }

    [HttpGet("worker/{workerId}/next")]
    public ActionResult<ExperimentResponse> GetNextForWorker(string workerId)
    {
        var experiment =
            _experimentRegistry.GetNextAssignedToWorker(workerId);

        if (experiment is null)
        {
            return NoContent();
        }

        return Ok(experiment);
    }

    [HttpPost("{id:guid}/progress")]
    public ActionResult<ExperimentResponse> ReportProgress(
        Guid id,
        ReportExperimentProgressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkerId))
        {
            return BadRequest("WorkerId is required.");
        }

        if (request.Attempt <= 0)
        {
            return BadRequest(
                "A valid execution attempt is required.");
        }

        if (request.CurrentStep < 0)
        {
            return BadRequest(
                "CurrentStep cannot be negative.");
        }

        var existingExperiment =
            _experimentRegistry.GetById(id);

        if (existingExperiment is null)
        {
            return NotFound(
                $"Experiment '{id}' was not found.");
        }

        if (existingExperiment.Status !=
            ExperimentStatus.Running)
        {
            return Conflict(
                $"Experiment '{id}' is not currently running.");
        }

        if (existingExperiment.AssignedWorkerId !=
            request.WorkerId)
        {
            return Conflict(
                $"Experiment '{id}' is not assigned to worker " +
                $"'{request.WorkerId}'.");
        }

        if (existingExperiment.Attempt != request.Attempt)
        {
            return Conflict(
                $"Experiment '{id}' is currently on attempt " +
                $"{existingExperiment.Attempt}, but progress for " +
                $"attempt {request.Attempt} was received.");
        }

        if (existingExperiment.CancellationRequested)
        {
            return Conflict(
                "Progress cannot be reported after cancellation " +
                "has been requested.");
        }

        if (request.CurrentStep > existingExperiment.MaxSteps)
        {
            return BadRequest(
                $"CurrentStep cannot be greater than MaxSteps " +
                $"({existingExperiment.MaxSteps}).");
        }

        if (existingExperiment.CurrentStep.HasValue &&
            request.CurrentStep <
            existingExperiment.CurrentStep.Value)
        {
            return Conflict(
                "CurrentStep cannot move backwards.");
        }

        var updated =
            _experimentRegistry.TryReportProgress(
                id,
                request.WorkerId,
                request.Attempt,
                request.CurrentStep,
                request.ProgressMetricsJson,
                out var updatedExperiment);

        if (!updated || updatedExperiment is null)
        {
            return Conflict(
                $"Progress for experiment '{id}' " +
                "could not be recorded.");
        }

        return Ok(updatedExperiment);
    }

    [HttpPost("{id:guid}/complete")]
    public ActionResult<ExperimentResponse> Complete(
        Guid id,
        CompleteExperimentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkerId))
        {
            return BadRequest("WorkerId is required.");
        }

        if (request.Attempt <= 0)
        {
            return BadRequest("A valid execution attempt is required.");
        }

        var existingExperiment = _experimentRegistry.GetById(id);

        if (existingExperiment is null)
        {
            return NotFound($"Experiment '{id}' was not found.");
        }

        if (existingExperiment.Status != ExperimentStatus.Running)
        {
            return Conflict(
                $"Experiment '{id}' is not currently running.");
        }

        if (existingExperiment.AssignedWorkerId != request.WorkerId)
        {
            return Conflict(
                $"Experiment '{id}' is not assigned to worker " +
                $"'{request.WorkerId}'.");
        }

        if (existingExperiment.Attempt != request.Attempt)
        {
            return Conflict(
                $"Experiment '{id}' is currently on attempt " +
                $"{existingExperiment.Attempt}, but result for attempt " +
                $"{request.Attempt} was received.");
        }

        var completed = _experimentRegistry.TryComplete(
            id,
            request.WorkerId,
            request.Attempt,
            request.Succeeded,
            request.WasCancelled,
            request.ResultMessage,
            out var finishedExperiment,
            request.MetricsJson,
            request.ExecutionDurationMs);

        if (!completed || finishedExperiment is null)
        {
            return Conflict(
                $"Experiment '{id}' could not be completed.");
        }

        if (existingExperiment.CancellationRequested !=
            request.WasCancelled)
        {
            return Conflict(
                "The completion result does not match the current " +
                "cancellation state.");
        }

        return Ok(finishedExperiment);
    }

    [HttpGet("{id:guid}/events")]
    public ActionResult<IReadOnlyCollection<ExperimentEventResponse>>
        GetEvents(Guid id)
    {
        var experiment = _experimentRegistry.GetById(id);

        if (experiment is null)
        {
            return NotFound($"Experiment '{id}' was not found.");
        }

        return Ok(_experimentRegistry.GetEvents(id));
    }

    [HttpPost("{id:guid}/cancel")]
    public ActionResult<ExperimentResponse> Cancel(Guid id)
    {
        var existingExperiment =
            _experimentRegistry.GetById(id);

        if (existingExperiment is null)
        {
            return NotFound(
                $"Experiment '{id}' was not found.");
        }

        if (existingExperiment.Status ==
            ExperimentStatus.Cancelled)
        {
            return Ok(existingExperiment);
        }

        if (existingExperiment.Status is
            ExperimentStatus.Completed or
            ExperimentStatus.Failed)
        {
            return Conflict(
                $"Experiment '{id}' cannot be cancelled because " +
                $"its status is '{existingExperiment.Status}'.");
        }

        var cancelled =
            _experimentRegistry.TryRequestCancellation(
                id,
                out var updatedExperiment);

        if (!cancelled || updatedExperiment is null)
        {
            return Conflict(
                $"Experiment '{id}' could not be cancelled.");
        }

        return Ok(updatedExperiment);
    }
    
}