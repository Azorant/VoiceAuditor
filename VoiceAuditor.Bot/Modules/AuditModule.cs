using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using VoiceAuditor.Database;

namespace VoiceAuditor.Bot.Modules;

public enum Activity
{
    Most,
    Least
}

public class AuditModule(DatabaseContext db) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("recent", "Show recent logs")]
    public async Task RecentCommand([Summary(description: "Show latest records for a specific user")] IUser? user = null)
    {
        await DeferAsync();
        var records = await db.AuditLogs.OrderByDescending(x => x.Id)
            .Where(x => user == null ? x.GuildId == Context.Guild.Id : x.GuildId == Context.Guild.Id && x.UserId == user.Id).Take(5).ToListAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Latest Records")
            .WithColor(Color.Blue);

        if (user != null) embed.WithDescription($"Showing the last {records.Count} records for {user.Mention}.");

        foreach (var record in records)
        {
            embed.AddField($"{TimestampTag.FormatFromDateTime(record.JoinedAt.Stupid(), TimestampTagStyles.Relative)}",
                $"{(user == null ? $"**Who**: <@{record.UserId}>\n" : "")}**Joined**: {TimestampTag.FormatFromDateTime(record.JoinedAt.Stupid(), TimestampTagStyles.ShortDateTime)}\n**Left**: {(record.LeftAt == null ? "still connected" : $"{TimestampTag.FormatFromDateTime(record.LeftAt.Value.Stupid(), TimestampTagStyles.ShortDateTime)}\n**Duration**: {record.Duration!.Value.Format()}")}");
        }

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("leaderboard", "Show the users with the most time in VC")]
    public async Task LeaderboardCommand(Activity activity = Activity.Most)
    {
        await DeferAsync();
        var records = await db.AuditLogs
            .Where(x => x.GuildId == Context.Guild.Id && x.Duration != null)
            .GroupBy(x => x.UserId)
            .ToListAsync();

        var top = records.Select(x => new { UserId = x.Key, Total = x.Sum(c => c.Duration!.Value.TotalSeconds) })
            .OrderByDescending(x => x.Total).ToList();

        top = activity switch
        {
            Activity.Most => top.Take(10).ToList(),
            Activity.Least => top.TakeLast(10).ToList(),
            _ => top
        };

        var embed = new EmbedBuilder()
            .WithTitle("Guild Leaderboard")
            .WithDescription($"Top 10 people with the {Enum.GetName(activity)!.ToLower()} time in VC.")
            .WithColor(Color.Blue);

        foreach (var record in top)
        {
            embed.AddField($"#{embed.Fields.Count + 1}", $"<@{record.UserId}> with {TimeSpan.FromSeconds(record.Total).Format()}");
        }

        await FollowupAsync(embed: embed.Build());
    }
}