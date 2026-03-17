using System.Security.Claims;
using ITPlotter.Application.DTOs.PrintStats;
using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public StatsController(IApplicationDbContext db) => _db = db;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<PrintStatsDto>> GetMyStats(CancellationToken ct)
    {
        var jobs = await _db.PrintJobs
            .Where(j => j.UserId == UserId && j.Status == PrintJobStatus.Completed)
            .ToListAsync(ct);

        var totalPages = jobs.Sum(j => j.Copies);

        var byFormat = jobs
            .GroupBy(j => j.PaperFormat)
            .Select(g => new FormatStatsDto(g.Key, g.Sum(j => j.Copies)))
            .OrderBy(f => f.Format)
            .ToList();

        var recent = jobs
            .GroupBy(j => DateOnly.FromDateTime(j.CompletedAt ?? j.CreatedAt))
            .Select(g => new DailyStatsDto(g.Key, g.Sum(j => j.Copies)))
            .OrderByDescending(d => d.Date)
            .Take(30)
            .ToList();

        return Ok(new PrintStatsDto(jobs.Count, totalPages, byFormat, recent));
    }
}
