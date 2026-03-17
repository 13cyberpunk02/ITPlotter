using System.Net.Http.Json;
using System.Text.Json;
using ITPlotter.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ITPlotter.Infrastructure.Services;

public class CupsApiService : ICupsService
{
    private readonly HttpClient _http;
    private readonly string _cupsBaseUrl;

    public CupsApiService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _cupsBaseUrl = configuration["Cups:BaseUrl"] ?? "http://cups:631";
        _http.BaseAddress = new Uri(_cupsBaseUrl);
    }

    public async Task<IReadOnlyList<CupsPrinterInfo>> GetPrintersAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/printers", ct);
        response.EnsureSuccessStatusCode();

        var printers = await response.Content.ReadFromJsonAsync<List<CupsPrinterInfo>>(ct);
        return printers ?? [];
    }

    public async Task<CupsPrinterInfo?> GetPrinterAsync(string printerName, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/printers/{Uri.EscapeDataString(printerName)}", ct);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<CupsPrinterInfo>(ct);
    }

    public async Task AddPrinterAsync(string printerName, string deviceUri, string driverUri, CancellationToken ct = default)
    {
        var payload = new { name = printerName, deviceUri, driverUri };
        var response = await _http.PostAsJsonAsync("/printers", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemovePrinterAsync(string printerName, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/printers/{Uri.EscapeDataString(printerName)}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> PrintFileAsync(string printerName, Stream fileStream, string fileName, PrintJobOptions options, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(options.Copies.ToString()), "copies");
        content.Add(new StringContent(options.PaperFormat.ToString()), "paperFormat");

        var response = await _http.PostAsync($"/printers/{Uri.EscapeDataString(printerName)}/print", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);

        // Try JSON first
        if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith('{'))
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(body);
                if (json.TryGetProperty("jobId", out var jobIdProp))
                    return jobIdProp.GetInt32();
                if (json.TryGetProperty("job-id", out var jobIdProp2))
                    return jobIdProp2.GetInt32();
            }
            catch (JsonException) { /* fall through */ }
        }

        // Try to extract job ID from text/HTML (e.g. "Job 123 created")
        var match = System.Text.RegularExpressions.Regex.Match(body, @"[Jj]ob[^\d]*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var extractedJobId))
            return extractedJobId;

        // CUPS returned 200 — job was accepted, return 0 as fallback
        return 0;
    }

    public async Task<CupsJobInfo?> GetJobStatusAsync(int jobId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/jobs/{jobId}", ct);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<CupsJobInfo>(ct);
    }

    public async Task CancelJobAsync(int jobId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/jobs/{jobId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
