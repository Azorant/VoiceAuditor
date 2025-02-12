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

public enum Range
{
    Day,
    Week,
    Month,
    Year,
    AllTime
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
    public async Task LeaderboardCommand(Range range = Range.Month, Activity activity = Activity.Most, bool showBots = false)
    {
        await DeferAsync();

        DateTime? date = range switch
        {
            Range.Day => DateTime.UtcNow.AddDays(-1),
            Range.Week => DateTime.UtcNow.AddDays(-7),
            Range.Month => DateTime.UtcNow.AddMonths(-1),
            Range.Year => DateTime.UtcNow.AddYears(-1),
            _ => null
        };

        var period = range switch
        {
            Range.Day => " in the past day",
            Range.Week => " in the past week",
            Range.Month => " in the past month",
            Range.Year => " in the past year",
            _ => string.Empty
        };

        var records = await db.AuditLogs
            .Include(x => x.User)
            .Where(x => x.GuildId == Context.Guild.Id && date == null || x.JoinedAt >= date)
            .Where(x => showBots || x.User.IsBot == false)
            .GroupBy(x => x.UserId)
            .ToListAsync();

        if (records.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("Guild Leaderboard")
                .WithDescription($"There's been no vc activity{period}.")
                .WithColor(Color.Gold)
                .Build());
            return;
        }

        var list = records
            .Select(x =>
                new
                {
                    UserId = x.Key,
                    Total = x.Sum(c => (c.LeftAt ?? DateTime.UtcNow).Subtract(c.JoinedAt).TotalSeconds)
                })
            .ToList();

        list = activity switch
        {
            Activity.Most => list.OrderByDescending(x => x.Total).Take(10).ToList(),
            Activity.Least => list.OrderBy(x => x.Total).Take(10).ToList(),
            _ => list
        };

        var embed = new EmbedBuilder()
            .WithTitle("Guild Leaderboard")
            .WithDescription($"Showing people with the {Enum.GetName(activity)!.ToLower()} time in VC{period}.")
            .WithColor(Color.Blue);

        foreach (var record in list)
        {
            embed.AddField($"#{embed.Fields.Count + 1}", $"<@{record.UserId}>\n{TimeSpan.FromSeconds(record.Total).Format()}");
        }

        var chunkSize = 10;
        var paginator = new StaticPaginatorBuilder()
            .AddUser(Context.User)
            .WithPages(list.Chunk(chunkSize)
                .Select((chunk, chunkIndex) => new PageBuilder()
                    .WithTitle("Guild Leaderboard")
                    .WithDescription($"Showing people with the {Enum.GetName(activity)!.ToLower()} time in VC{period}.")
                    .WithFields(chunk
                        .Select((record, recordIndex) => new EmbedFieldBuilder()
                            .WithName($"#{chunkIndex * chunkSize + recordIndex + 1:N0}")
                            .WithValue($"<@{record.UserId}>\n{TimeSpan.FromSeconds(record.Total).Format()}")))
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
                .WithColor(Color.Gold)
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