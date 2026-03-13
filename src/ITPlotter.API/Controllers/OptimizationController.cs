using System.Security.Claims;
using ITPlotter.Application.Interfaces;
using ITPlotter.Infrastructure.PdfProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OptimizationController : ControllerBase
{
    private readonly DocumentOptimizationService _optimization;
    private readonly IApplicationDbContext _db;

    public OptimizationController(DocumentOptimizationService optimization, IApplicationDbContext db)
    {
        _optimization = optimization;
        _db = db;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Оптимизирует загруженный PDF документ для плоттерной печати.
    /// Определяет формат, растеризует тяжёлые PDF, склеивает дубли, поворачивает для экономии бумаги.
    /// </summary>
    [HttpPost("{documentId:guid}")]
    public async Task<IActionResult> OptimizeDocument(Guid documentId, CancellationToken ct)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == UserId, ct);

        if (document == null)
            return NotFound(new { message = "Документ не найден." });

        if (document.Format != Domain.Enums.DocumentFormat.Pdf)
            return BadRequest(new { message = "Оптимизация доступна только для PDF файлов." });

        try
        {
            var result = await _optimization.OptimizeDocumentAsync(
                document.S3Key, document.OriginalFileName, ct);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Ошибка оптимизации: {ex.Message}" });
        }
    }
}
