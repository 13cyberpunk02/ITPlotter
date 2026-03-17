using ITPlotter.Application.Interfaces;
using ITPlotter.Application.Services;
using ITPlotter.Domain.Interfaces;
using ITPlotter.Infrastructure.Data;
using ITPlotter.Infrastructure.PdfProcessing;
using ITPlotter.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace ITPlotter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddMinio(client =>
        {
            client
                .WithEndpoint(configuration["Minio:Endpoint"] ?? "localhost:9000")
                .WithCredentials(
                    configuration["Minio:AccessKey"] ?? "minioadmin",
                    configuration["Minio:SecretKey"] ?? "minioadmin")
                .WithSSL(bool.Parse(configuration["Minio:UseSSL"] ?? "false"));
        });

        services.AddScoped<IStorageService, MinioStorageService>();

        services.AddHttpClient<ICupsService, CupsApiService>();

        services.AddScoped<AuthService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<PrinterService>();
        services.AddScoped<PrintJobService>();
        services.AddScoped<ITPlotter.Infrastructure.Services.AutoPrintService>();

        // PDF optimization pipeline
        services.AddScoped<FormatDetector>();
        services.AddScoped<PdfRasterizer>();
        services.AddScoped<PrintOptimizer>();
        services.AddScoped<PdfProcessor>();
        services.AddScoped<DocumentOptimizationService>();

        return services;
    }
}
