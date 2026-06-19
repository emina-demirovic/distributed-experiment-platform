using Contracts;
using Coordinator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/experiments")]
public sealed class ExperimentsController : ControllerBase
{
    private readonly ExperimentRegistry _experimentRegistry;

    public ExperimentsController(ExperimentRegistry experimentRegistry)
    {
        _experimentRegistry = experimentRegistry;
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
}