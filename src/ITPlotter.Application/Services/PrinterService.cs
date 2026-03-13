using ITPlotter.Application.DTOs.Printers;
using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Entities;
using ITPlotter.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.Application.Services;

public class PrinterService
{
    private readonly IApplicationDbContext _db;
    private readonly ICupsService _cups;

    public PrinterService(IApplicationDbContext db, ICupsService cups)
    {
        _db = db;
        _cups = cups;
    }

    public async Task<List<PrinterDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Printers
            .OrderBy(p => p.Name)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
    }

    public async Task<PrinterDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var printer = await _db.Printers.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Принтер не найден.");
        return ToDto(printer);
    }

    public async Task<PrinterDto> CreateAsync(CreatePrinterRequest request, CancellationToken ct = default)
    {
        await _cups.AddPrinterAsync(request.CupsName, request.DeviceUri, request.DriverUri, ct);

        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CupsName = request.CupsName,
            Location = request.Location,
            Type = request.Type,
            MaxPaperFormat = request.MaxPaperFormat
        };

        _db.Printers.Add(printer);
        await _db.SaveChangesAsync(ct);

        return ToDto(printer);
    }

    public async Task<PrinterDto> UpdateAsync(Guid id, UpdatePrinterRequest request, CancellationToken ct = default)
    {
        var printer = await _db.Printers.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Принтер не найден.");

        if (request.Name is not null) printer.Name = request.Name;
        if (request.Location is not null) printer.Location = request.Location;
        if (request.Type.HasValue) printer.Type = request.Type.Value;
        if (request.MaxPaperFormat.HasValue) printer.MaxPaperFormat = request.MaxPaperFormat.Value;

        await _db.SaveChangesAsync(ct);
        return ToDto(printer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var printer = await _db.Printers.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Принтер не найден.");

        await _cups.RemovePrinterAsync(printer.CupsName, ct);
        _db.Printers.Remove(printer);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncStatusAsync(Guid id, CancellationToken ct = default)
    {
        var printer = await _db.Printers.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Принтер не найден.");

        var cupsInfo = await _cups.GetPrinterAsync(printer.CupsName, ct);
        if (cupsInfo is null) return;

        printer.TonerLevelPercent = cupsInfo.TonerLevel;
        printer.InkLevelPercent = cupsInfo.InkLevel;
        printer.PaperRemaining = cupsInfo.PaperRemaining;
        printer.LastStatusUpdate = DateTime.UtcNow;

        printer.Status = cupsInfo.State switch
        {
            "idle" => Domain.Enums.PrinterStatus.Idle,
            "processing" => Domain.Enums.PrinterStatus.Printing,
            "stopped" => Domain.Enums.PrinterStatus.Error,
            _ => printer.Status
        };

        await _db.SaveChangesAsync(ct);
    }

    private static PrinterDto ToDto(Printer p) =>
        new(p.Id, p.Name, p.CupsName, p.Location, p.Type, p.Status,
            p.MaxPaperFormat, p.TonerLevelPercent, p.InkLevelPercent, p.PaperRemaining);
}
