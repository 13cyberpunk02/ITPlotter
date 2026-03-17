using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using ITPlotter.API.Filters;
using ITPlotter.Infrastructure;
using ITPlotter.Domain.Interfaces;
using ITPlotter.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ITPlotter.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddValidatorsFromAssemblyContaining<ITPlotter.Application.Validators.RegisterRequestValidator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    // Seed default admin user if no admins exist
    if (!await db.Users.AnyAsync(u => u.Role == ITPlotter.Domain.Enums.UserRole.Admin))
    {
        db.Users.Add(new ITPlotter.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            FirstName = "Admin",
            LastName = "Admin",
            Email = "admin@itplotter.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
            Role = ITPlotter.Domain.Enums.UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
    if (storage is MinioStorageService minioStorage)
        await minioStorage.EnsureBucketExistsAsync();
}

app.Run();
