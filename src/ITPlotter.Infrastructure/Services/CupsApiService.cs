using System.Diagnostics;
using System.Text.RegularExpressions;
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
        // Extract host from URL like "http://cups:631"
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

            var args = $"-h {_cupsServer} -d {printerName} -n {options.Copies} -t \"{fileName}\" {tempFile}";
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

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(string command, string arguments, CancellationToken ct)
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
