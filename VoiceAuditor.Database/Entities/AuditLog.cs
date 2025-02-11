using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace VoiceAuditor.Database.Entities;

[Index(nameof(UserId), nameof(GuildId))]
public class AuditLog
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [ForeignKey(nameof(User))]
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public virtual User User { get; set; } = null!;
}
