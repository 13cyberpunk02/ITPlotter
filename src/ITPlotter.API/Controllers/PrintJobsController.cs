using System.Security.Claims;
using ITPlotter.Application.DTOs.PrintJobs;
using ITPlotter.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITPlotter.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PrintJobsController : ControllerBase
{
    private readonly PrintJobService _printJobs;

    public PrintJobsController(PrintJobService printJobs) => _printJobs = printJobs;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<ActionResult<PrintJobDto>> Create([FromBody] CreatePrintJobRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _printJobs.CreateAsync(UserId, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<PrintJobDto>>> GetAll(CancellationToken ct)
    {
        var result = await _printJobs.GetUserJobsAsync(UserId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrintJobDto>> GetStatus(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _printJobs.GetJobStatusAsync(UserId, id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            await _printJobs.CancelAsync(UserId, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
