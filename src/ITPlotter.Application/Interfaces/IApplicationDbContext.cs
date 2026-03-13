using ITPlotter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Document> Documents { get; }
    DbSet<Printer> Printers { get; }
    DbSet<PrintJob> PrintJobs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
