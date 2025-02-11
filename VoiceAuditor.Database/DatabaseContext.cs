using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using VoiceAuditor.Database.Entities;

namespace VoiceAuditor.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<User> Users { get; set; }

    public async Task AssertUser(ulong id, bool isBot)
    {
        var exists = await Users.FirstOrDefaultAsync(x => x.Id == id);
        if (exists != null) return;
        await AddAsync(new User
        {
            Id = id,
            IsBot = isBot,
        });
        await SaveChangesAsync();
    }

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