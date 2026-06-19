using Contracts;
using Coordinator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/workers")]
public sealed class WorkersController : ControllerBase
{
    private readonly WorkerRegistry _workerRegistry;

    public WorkersController(WorkerRegistry workerRegistry)
    {
        _workerRegistry = workerRegistry;
    }

    [HttpPost("register")]
    public ActionResult<WorkerStatusResponse> Register(
        WorkerRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkerId))
        {
            return BadRequest("WorkerId is required.");
        }

        var worker = _workerRegistry.Register(request.WorkerId);

        return Ok(worker);
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<WorkerStatusResponse>> GetAll()
    {
        return Ok(_workerRegistry.GetAll());
    }
}