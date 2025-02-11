using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using VoiceAuditor.Database.Entities;

namespace VoiceAuditor.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs { get; set; }

    public void ApplyMigrations()
    {
        var pending = Database.GetPendingMigrations().ToList();
        if (pending.Any())
        {
            Log.Information($"Applying {pending.Count} migrations: {string.Join(',', pending)}");
            Database.Migrate();
            Log.Information("Migrations applied");
        }
        else
        {
            Log.Information("No migrations to apply.");
        }
    }
};