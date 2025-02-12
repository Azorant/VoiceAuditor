using Discord;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.EntityFrameworkCore;
using VoiceAuditor.Database;

namespace VoiceAuditor.Bot.Modules;

public enum Activity
{
    Most,
    Least
}

public class AuditModule(DatabaseContext db, InteractiveService interactiveService) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("recent", "Show recent logs")]
    public async Task RecentCommand([Summary(description: "Show latest records for a specific user")] IUser? user = null, bool showBots = false)
    {
        await DeferAsync();
        var records = await db.AuditLogs.Include(x => x.User).OrderByDescending(x => x.Id)
            .Where(x => user == null ? x.GuildId == Context.Guild.Id : x.GuildId == Context.Guild.Id && x.UserId == user.Id)
            .Where(x => user != null || showBots || x.User.IsBot == false).Take(5).ToListAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Latest Records")
            .WithColor(Color.Blue);

        if (user != null) embed.WithDescription($"Showing the last {records.Count} records for {user.Mention}.");

        foreach (var record in records)
        {
            var leftAt = record.LeftAt ?? DateTime.UtcNow;
            embed.AddField($"{TimestampTag.FormatFromDateTime(record.JoinedAt.Stupid(), TimestampTagStyles.Relative)}",
                $"{(user == null ? $"**Who**: <@{record.UserId}>\n" : "")}**Joined**: {TimestampTag.FormatFromDateTime(record.JoinedAt.Stupid(), TimestampTagStyles.ShortDateTime)}\n**Left**: {(record.LeftAt == null ? "still connected" : $"{TimestampTag.FormatFromDateTime(record.LeftAt.Value.Stupid(), TimestampTagStyles.ShortDateTime)}")}\n**Duration**: {leftAt.Subtract(record.JoinedAt).Format()}");
        }

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("leaderboard", "Show the users with the most time in VC")]
    public async Task LeaderboardCommand(Activity activity = Activity.Most, bool showBots = false)
    {
        await DeferAsync();
        var records = await db.AuditLogs
            .Include(x => x.User)
            .Where(x => x.GuildId == Context.Guild.Id)
            .Where(x => showBots || x.User.IsBot == false)
            .GroupBy(x => x.UserId)
            .ToListAsync();

        var top = records
            .Select(x =>
                new
                {
                    UserId = x.Key,
                    Total = x.Sum(c => (c.LeftAt ?? DateTime.UtcNow).Subtract(c.JoinedAt).TotalSeconds)
                })
            .ToList();

        top = activity switch
        {
            Activity.Most => top.OrderByDescending(x => x.Total).Take(10).ToList(),
            Activity.Least => top.OrderBy(x => x.Total).Take(10).ToList(),
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

    [SlashCommand("audit", "Show users who haven't joined vc in x days")]
    public async Task AuditCommand([MaxValue(365), MinValue(1)] int days = 30, bool showBots = false)
    {
        await DeferAsync();
        var members = Context.Guild.Users.Where(x => showBots || x.IsBot == false).Select(x => x.Id).ToList();
        var records = await db.AuditLogs.Where(x => x.JoinedAt >= DateTime.UtcNow.AddDays(-days)).Select(x => x.UserId).ToListAsync();
        var audited = members.Where(x => !records.Contains(x)).Select(x => $"<@{x}>").ToList();
        if (audited.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("Audit Results")
                .WithDescription($"There are no people that haven't joined vc in {days} day{Extensions.Plural(days)}.")
                .WithColor(Color.Blue)
                .Build());
            return;
        }

        var paginator = new StaticPaginatorBuilder()
            .AddUser(Context.User)
            .WithPages(audited.Chunk(25)
                .Select(chunk => new PageBuilder()
                    .WithTitle("Audit Results")
                    .WithDescription($"List of people that haven't joined vc in {days} day{Extensions.Plural(days)}.\n\n{string.Join("\n", chunk)}")
                    .WithColor(Color.Blue)))
            .AddOption(new Emoji("◀"), PaginatorAction.Backward, ButtonStyle.Secondary)
            .AddOption(context => new PaginatorButton(PaginatorAction.Backward, null,
                $"Page {context.CurrentPageIndex + 1} / {context.MaxPageIndex + 1}", ButtonStyle.Primary, true))
            .AddOption(new Emoji("▶"), PaginatorAction.Forward, ButtonStyle.Secondary)
            .AddOption(new Emoji("🔢"), PaginatorAction.Jump, ButtonStyle.Secondary)
            .WithFooter(PaginatorFooter.None)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredUpdateMessage);
    }
}