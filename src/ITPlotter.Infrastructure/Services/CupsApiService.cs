using System.Diagnostics;
using System.Text.RegularExpressions;
using ITPlotter.Domain.Enums;
using ITPlotter.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITPlotter.Infrastructure.Services;

public class CupsApiService : ICupsService
{
    private readonly string _cupsServer;
    private readonly ILogger<CupsApiService> _logger;

    public CupsApiService(HttpClient http, IConfiguration configuration, ILogger<CupsApiService> logger)
    {
        var baseUrl = configuration["Cups:BaseUrl"] ?? "http://cups:631";
        var uri = new Uri(baseUrl);
        _cupsServer = $"{uri.Host}:{uri.Port}";
        _logger = logger;
    }

    public async Task<IReadOnlyList<CupsPrinterInfo>> GetPrintersAsync(CancellationToken ct = default)
    {
        var (exitCode, output) = await RunCommandAsync("lpstat", $"-h {_cupsServer} -p -d", ct);
        if (exitCode != 0)
        {
            _logger.LogWarning("lpstat failed: {Output}", output);
            return [];
        }

        var printers = new List<CupsPrinterInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: "printer PrinterName is idle. ..." or "printer PrinterName disabled since ..."
            var match = Regex.Match(line, @"^printer\s+(\S+)\s+(.*)$");
            if (!match.Success) continue;

            var name = match.Groups[1].Value;
            var rest = match.Groups[2].Value.ToLowerInvariant();

            var state = "idle";
            if (rest.Contains("printing")) state = "processing";
            else if (rest.Contains("disabled") || rest.Contains("stopped")) state = "stopped";

            printers.Add(new CupsPrinterInfo
            {
                Name = name,
                State = state,
                IsAcceptingJobs = !rest.Contains("not accepting"),
                StateMessage = rest
            });
        }

        return printers;
    }

    public async Task<CupsPrinterInfo?> GetPrinterAsync(string printerName, CancellationToken ct = default)
    {
        var (exitCode, output) = await RunCommandAsync("lpstat", $"-h {_cupsServer} -p {printerName}", ct);
        if (exitCode != 0) return null;

        var match = Regex.Match(output, @"^printer\s+(\S+)\s+(.*)$", RegexOptions.Multiline);
        if (!match.Success) return null;

        var rest = match.Groups[2].Value.ToLowerInvariant();
        var state = "idle";
        if (rest.Contains("printing")) state = "processing";
        else if (rest.Contains("disabled") || rest.Contains("stopped")) state = "stopped";

        return new CupsPrinterInfo
        {
            Name = printerName,
            State = state,
            IsAcceptingJobs = !rest.Contains("not accepting"),
            StateMessage = rest
        };
    }

    public async Task AddPrinterAsync(string printerName, string deviceUri, string driverUri, CancellationToken ct = default)
    {
        var driver = string.IsNullOrWhiteSpace(driverUri) ? "everywhere" : driverUri;
        var args = $"-h {_cupsServer} -p {printerName} -v {deviceUri} -m {driver} -E";

        var (exitCode, output) = await RunCommandAsync("lpadmin", args, ct);
        if (exitCode != 0)
            throw new InvalidOperationException($"Не удалось добавить принтер в CUPS: {output}");

        _logger.LogInformation("Принтер {Printer} добавлен в CUPS", printerName);
    }

    public async Task RemovePrinterAsync(string printerName, CancellationToken ct = default)
    {
        var (exitCode, output) = await RunCommandAsync("lpadmin", $"-h {_cupsServer} -x {printerName}", ct);
        if (exitCode != 0)
            _logger.LogWarning("Не удалось удалить принтер {Printer} из CUPS: {Output}", printerName, output);
    }

    public async Task<int> PrintFileAsync(string printerName, Stream fileStream, string fileName, PrintJobOptions options, CancellationToken ct = default)
    {
        // Save stream to temp file for lp command
        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fs = File.Create(tempFile))
            {
                await fileStream.CopyToAsync(fs, ct);
            }

            // Если заданы реальные размеры оптимизированного PDF — используем custom media
            // Это важно для рулонных плоттеров: PDF уже повёрнут/склеен оптимизатором
            string cupsMedia;
            if (options.WidthMm.HasValue && options.LengthMm.HasValue)
            {
                var w = (int)Math.Ceiling(options.WidthMm.Value);
                var l = (int)Math.Ceiling(options.LengthMm.Value);
                cupsMedia = $"custom_{w}x{l}mm";
            }
            else
            {
                cupsMedia = PaperFormatToCupsMedia(options.PaperFormat);
            }

            var args = $"-h {_cupsServer} -d {printerName} -n {options.Copies} -o media={cupsMedia} -o scaling=100 -o position=center -t \"{fileName}\" {tempFile}";
            var (exitCode, output) = await RunCommandAsync("lp", args, ct);

            if (exitCode != 0)
                throw new InvalidOperationException($"Ошибка печати через CUPS: {output}");

            // Parse job ID from output like "request id is PrinterName-123 (1 file(s))"
            var match = Regex.Match(output, @"request id is \S+-(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var jobId))
                return jobId;

            _logger.LogWarning("Не удалось извлечь ID задания из ответа lp: {Output}", output);
            return 0;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    public async Task<CupsJobInfo?> GetJobStatusAsync(int jobId, CancellationToken ct = default)
    {
        if (jobId <= 0) return null;

        var (exitCode, output) = await RunCommandAsync("lpstat", $"-h {_cupsServer} -W all -o", ct);
        if (exitCode != 0) return null;

        // Format: "PrinterName-123 user  1024 Mon 01 Jan 2025 12:00:00"
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"^(\S+)-(\d+)\s+");
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups[2].Value, out var id) || id != jobId) continue;

            return new CupsJobInfo
            {
                JobId = jobId,
                PrinterName = match.Groups[1].Value,
                State = "processing"
            };
        }

        // Job not in active list — check if it completed
        var (exitCode2, output2) = await RunCommandAsync("lpstat", $"-h {_cupsServer} -W completed -o", ct);
        if (exitCode2 == 0)
        {
            foreach (var line in output2.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(line, @"^(\S+)-(\d+)\s+");
                if (!match.Success) continue;
                if (!int.TryParse(match.Groups[2].Value, out var id) || id != jobId) continue;

                return new CupsJobInfo
                {
                    JobId = jobId,
                    PrinterName = match.Groups[1].Value,
                    State = "completed"
                };
            }
        }

        return null;
    }

    public async Task CancelJobAsync(int jobId, CancellationToken ct = default)
    {
        if (jobId <= 0) return;

        var (exitCode, output) = await RunCommandAsync("cancel", $"-h {_cupsServer} {jobId}", ct);
        if (exitCode != 0)
            _logger.LogWarning("Не удалось отменить задание {JobId}: {Output}", jobId, output);
    }

    private static string PaperFormatToCupsMedia(PaperFormat format) => format switch
    {
        PaperFormat.A4 => "iso_a4_210x297mm",
        PaperFormat.A3 => "iso_a3_297x420mm",
        PaperFormat.A2 => "iso_a2_420x594mm",
        PaperFormat.A1 => "iso_a1_594x841mm",
        PaperFormat.A0 => "iso_a0_841x1189mm",
        // Extended formats — custom media with exact dimensions
        PaperFormat.A4x3 => "custom_297x630mm_297x630mm",
        PaperFormat.A4x4 => "custom_297x841mm_297x841mm",
        PaperFormat.A4x5 => "custom_297x1051mm_297x1051mm",
        PaperFormat.A4x6 => "custom_297x1261mm_297x1261mm",
        PaperFormat.A4x7 => "custom_297x1471mm_297x1471mm",
        PaperFormat.A4x8 => "custom_297x1682mm_297x1682mm",
        PaperFormat.A4x9 => "custom_297x1892mm_297x1892mm",
        PaperFormat.A3x3 => "custom_420x891mm_420x891mm",
        PaperFormat.A3x4 => "custom_420x1189mm_420x1189mm",
        PaperFormat.A3x5 => "custom_420x1486mm_420x1486mm",
        PaperFormat.A3x6 => "custom_420x1783mm_420x1783mm",
        PaperFormat.A3x7 => "custom_420x2080mm_420x2080mm",
        PaperFormat.A2x3 => "custom_594x1261mm_594x1261mm",
        PaperFormat.A2x4 => "custom_594x1682mm_594x1682mm",
        PaperFormat.A2x5 => "custom_594x2102mm_594x2102mm",
        PaperFormat.A1x3 => "custom_841x1783mm_841x1783mm",
        PaperFormat.A1x4 => "custom_841x2378mm_841x2378mm",
        PaperFormat.A0x2 => "custom_1189x1682mm_1189x1682mm",
        PaperFormat.A0x3 => "custom_1189x2523mm_1189x2523mm",
        _ => "iso_a4_210x297mm"
    };

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(
        string command, string arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        return (process.ExitCode, output.Trim());
    }
}
