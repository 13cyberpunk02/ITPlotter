using System.Security.Claims;
using ITPlotter.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITPlotter.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AutoPrintController : ControllerBase
{
    private readonly AutoPrintService _autoPrint;

    public AutoPrintController(AutoPrintService autoPrint) => _autoPrint = autoPrint;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Загружает PDF, оптимизирует, находит подходящие принтеры и отправляет на печать автоматически.
    /// </summary>
    [HttpPost("{documentId:guid}")]
    public async Task<IActionResult> Print(Guid documentId, CancellationToken ct)
    {
        try
        {
            var result = await _autoPrint.ProcessAndPrintAsync(UserId, documentId, ct);
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
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Ошибка при печати: {ex.Message}" });
        }
    }
}
