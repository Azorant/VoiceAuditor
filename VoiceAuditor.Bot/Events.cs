using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VoiceAuditor.Database;
using VoiceAuditor.Database.Entities;

namespace VoiceAuditor.Bot;

public class Events(DatabaseContext db, DiscordSocketClient client)
{
    public Task OnGuildJoined(SocketGuild guild)
    {
        Task.Run(async () =>
        {
            if (!ulong.TryParse(Environment.GetEnvironmentVariable("GUILD_CHANNEL"), out var channelId)) return;

            if (client.GetChannel(channelId) is not SocketTextChannel channel || channel.GetChannelType() != ChannelType.Text) return;

            var user = await client.GetUserAsync(guild.OwnerId);
            var owner = user == null
                ? $"**Owner ID:** {guild.OwnerId}"
                : $"**Owner:** {Format.UsernameAndDiscriminator(user, false)}\n**Owner ID:** {user.Id}";

            await channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("Joined guild")
                .WithDescription($"**Name:** {guild.Name}\n**ID:** {guild.Id}\n{owner}\n**Members:** {guild.MemberCount}\n**Created:** {guild.CreatedAt:f}")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(guild.IconUrl).Build());
        });
        return Task.CompletedTask;
    }

    public Task OnGuildLeft(SocketGuild guild)
    {
        Task.Run(async () =>
        {
            if (!ulong.TryParse(Environment.GetEnvironmentVariable("GUILD_CHANNEL"), out var channelId)) return;

            if (client.GetChannel(channelId) is not SocketTextChannel channel || channel.GetChannelType() != ChannelType.Text) return;

            var user = await client.GetUserAsync(guild.OwnerId);
            var owner = user == null
                ? $"**Owner ID:** {guild.OwnerId}"
                : $"**Owner:** {Format.UsernameAndDiscriminator(user, false)}\n**Owner ID:** {user.Id}";

            await channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("Left guild")
                .WithDescription($"**Name:** {guild.Name}\n**ID:** {guild.Id}\n{owner}\n**Members:** {guild.MemberCount}\n**Created:** {guild.CreatedAt:f}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(guild.IconUrl).Build());
        });
        return Task.CompletedTask;
    }

    public Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldChannel, SocketVoiceState newChannel)
    {
        Task.Run(async () =>
        {
            await db.AssertUser(user.Id, user.IsBot);
            // Moved channels so we don't care unless its afk channel then stop logging
            if (oldChannel.VoiceChannel != null && newChannel.VoiceChannel != null)
            {
                if (newChannel.VoiceChannel.Id == newChannel.VoiceChannel.Guild.AFKChannel.Id)
                {
                    var record = await db.AuditLogs.OrderBy(x => x.Id).LastOrDefaultAsync(x => x.GuildId == oldChannel.VoiceChannel.Guild.Id && x.UserId == user.Id);
                    if (record == null) return;
                    record.LeftAt = DateTime.UtcNow;
                    db.Update(record);
                    await db.SaveChangesAsync();
                }
                else if (oldChannel.VoiceChannel.Id == oldChannel.VoiceChannel.Guild.AFKChannel.Id)
                {
                    var record = await db.AuditLogs.OrderBy(x => x.Id)
                        .LastOrDefaultAsync(x => x.GuildId == oldChannel.VoiceChannel.Guild.Id && x.UserId == user.Id && x.LeftAt == null);
                    if (record != null) return;
                    await db.AddAsync(new AuditLog
                    {
                        UserId = user.Id,
                        GuildId = newChannel.VoiceChannel.Guild.Id
                    });
                    await db.SaveChangesAsync();
                }
            }

            // Joined channel
            if (oldChannel.VoiceChannel == null && newChannel.VoiceChannel != null)
            {
                await db.AddAsync(new AuditLog
                {
                    UserId = user.Id,
                    GuildId = newChannel.VoiceChannel.Guild.Id
                });
                await db.SaveChangesAsync();
            }

            // Left channel
            if (newChannel.VoiceChannel == null && oldChannel.VoiceChannel != null)
            {
                var record = await db.AuditLogs.OrderBy(x => x.Id).LastOrDefaultAsync(x => x.GuildId == oldChannel.VoiceChannel.Guild.Id && x.UserId == user.Id);

                // Bot possibly offline when first joining
                if (record == null)
                {
                    await db.AddAsync(new AuditLog
                    {
                        UserId = user.Id,
                        GuildId = oldChannel.VoiceChannel.Guild.Id,
                        LeftAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    record.LeftAt = DateTime.UtcNow;
                    db.Update(record);
                }

                await db.SaveChangesAsync();
            }
        }).ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Log.Error(t.Exception, "Failed to update log");
            }
        });
        return Task.CompletedTask;
    }
}