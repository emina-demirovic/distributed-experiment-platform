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

        var experiment = _experimentRegistry.Create(request.Name);

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
}