using System.ComponentModel.DataAnnotations;

namespace VoiceAuditor.Database.Entities;

public class User
{
    [Key]
    public ulong Id {get; set;}
    public bool IsBot { get; set; }
}