using ITPlotter.Application.DTOs.Printers;
using ITPlotter.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITPlotter.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class PrintersController : ControllerBase
{
    private readonly PrinterService _printers;

    public PrintersController(PrinterService printers) => _printers = printers;

    [HttpGet]
    public async Task<ActionResult<List<PrinterDto>>> GetAll(CancellationToken ct)
    {
        var result = await _printers.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrinterDto>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _printers.GetByIdAsync(id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<ActionResult<PrinterDto>> Create([FromBody] CreatePrinterRequest request, CancellationToken ct)
    {
        var result = await _printers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PrinterDto>> Update(Guid id, [FromBody] UpdatePrinterRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _printers.UpdateAsync(id, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _printers.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/sync-status")]
    public async Task<IActionResult> SyncStatus(Guid id, CancellationToken ct)
    {
        try
        {
            await _printers.SyncStatusAsync(id, ct);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
