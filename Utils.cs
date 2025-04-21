using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using FastCache;
using Markdig;

namespace discord_forumsync
{
    public partial class Utils
    {
        public static string FormatMessageContent(DiscordMessage message, DiscordGuild? guild)
        {
            if (message == null) return "";
            string messageContent = HtmlFilterRegex().Replace(message.Content, string.Empty);
            foreach (var user in message.MentionedUsers) messageContent = ReplaceMentionWithUsername(messageContent, user);
            messageContent = ParseEmojis(messageContent);
            messageContent = Markdown.ToHtml(messageContent, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());

            foreach (var attachment in message.Attachments)
            {
                if (attachment.ProxyUrl != null && attachment.Width != null)
                {
                    messageContent += $"<img src='{attachment.ProxyUrl}' class='attachment-img' loading='lazy' attachment-id='{attachment.Id}'>";
                }
                else if (attachment.Width == null && attachment.FileName != null)
                {
                    messageContent += @$"<div class='attachment-file' attachment-id='{attachment.Id}'>
                        <img src='/fileimg.png'>
                        <div class='attachment-file-info'>
                            <a href='{attachment.Url}' target='_blank'>{attachment.FileName}</a>
                            <span class='attachment-file-size'>{ConvertBytes(attachment.FileSize)}</span>
                        </div>
                    </div>";
                }
            }

            foreach (var role in message.MentionedRoles)
            {
                messageContent = messageContent.Replace(role.Mention, $"<span class='mention mention-role' style='background: {ConvertHexToRgba(role.Color.ToString(), 0.5f)}'>@{role.Name}</span>");
            }

            foreach (var sticker in message.Stickers ?? [])
            {
                if (sticker.StickerUrl != null) messageContent += $"<img src='{sticker.BannerUrl}' class='attachment-img' loading='lazy'>";
            }
            return messageContent;
        }

        public async static Task<List<DiscordForumChannel>> GetAllGuildForums(DiscordGuild guild, string? syncId)
        {
            if (Cached<List<DiscordForumChannel>>.TryGet(guild.Id, out var cachedChannels) && syncId == null)
            {
                return cachedChannels;
            }

            var channels = await guild.GetChannelsAsync();
            List<DiscordForumChannel> forumChannels = [];

            foreach (var channel in channels)
            {
                if (channel.Type == DiscordChannelType.GuildForum) forumChannels.Add((DiscordForumChannel)channel);
            }

            Cached<List<DiscordForumChannel>>.Save(guild.Id, forumChannels, TimeSpan.FromMinutes(60));
            Console.WriteLine($"fetched {forumChannels.Count} forums for guild '{guild.Id}'");
            return forumChannels;
        }

        public async static Task<List<DiscordThreadChannel>?> GetThreadsAsync(DiscordChannel channel)
        {
            if (Program.discordClient == null) return null;
            if (Cached<List<DiscordThreadChannel>>.TryGet(channel.Id, out var threads))
            {
                return threads;
            }

            List<DiscordThreadChannel> fetchedThreads = [];
            var activeThreadsResult = await channel.Guild.ListActiveThreadsAsync();
            var archivedThreadsResult = await channel.ListPublicArchivedThreadsAsync();

            fetchedThreads = [.. activeThreadsResult.Threads.Where(thread => thread.ParentId == channel.Id), .. archivedThreadsResult.Threads.ToList()];

            Cached<List<DiscordThreadChannel>>.Save(channel.Id, fetchedThreads, TimeSpan.FromMinutes(60));
            Console.WriteLine($"fetched {fetchedThreads.Count} threads for channel '{channel.Id}'");

            return fetchedThreads;
        }

        public async static Task<DiscordGuild?> GetGuildAsync(ulong guildId)
        {
            if (Program.discordClient == null) return null;

            if (Cached<DiscordGuild>.TryGet(guildId, out var guild))
            {
                return guild;
            }

            DiscordGuild fetchedGuild = await Program.discordClient.GetGuildAsync(guildId);

            if (fetchedGuild != null)
            {
                Cached<DiscordGuild>.Save(guildId, fetchedGuild, TimeSpan.FromMinutes(60));
            }

            Console.WriteLine($"fetched guild '{guildId}'");
            return fetchedGuild;
        }

        public async static Task<DiscordChannel?> GetChannelAsync(ulong channelId)
        {
            if (Program.discordClient == null) return null;

            if (Cached<DiscordChannel>.TryGet(channelId, out var channel))
            {
                return channel;
            }

            DiscordChannel fetchedChannel = await Program.discordClient.GetChannelAsync(channelId);

            Cached<DiscordChannel>.Save(channelId, fetchedChannel, TimeSpan.FromMinutes(60));
            Console.WriteLine($"fetched channel '{channelId}'");

            return fetchedChannel;
        }

        public async static Task<List<DiscordGuild>?> GetAllGuildsAsnyc(string? syncId)
        {
            if (Program.discordClient == null) return null;
            if (Cached<List<DiscordGuild>>.TryGet("guilds", out var cachedGuilds) && syncId == null)
            {
                return cachedGuilds;
            }

            List<DiscordGuild> guilds = [];

            await foreach (var guild in Program.discordClient.GetGuildsAsync())
            {
                guilds.Add(guild);
            }

            Cached<List<DiscordGuild>>.Save("guilds", guilds, TimeSpan.FromMinutes(60));
            Console.WriteLine($"fetched {guilds.Count} guilds");

            return guilds;
        }

        public async static Task<DiscordUser?> GetUserAsync(ulong userId)
        {
            if (Program.discordClient == null) return null;

            if (Cached<DiscordUser>.TryGet(userId, out var guild))
            {
                return guild;
            }

            DiscordUser fetchedUser = await Program.discordClient.GetUserAsync(userId);

            if (fetchedUser != null)
            {
                Cached<DiscordUser>.Save(userId, fetchedUser, TimeSpan.FromMinutes(60));
            }

            Console.WriteLine($"fetched user '{userId}' | '{fetchedUser?.Username}'");
            return fetchedUser;
        }

        public static string ReplaceMentionWithUsername(string messageContent, DiscordUser user)
        {
            return messageContent.Replace($"<@{user.Id}>", $"<span class='mention'>@{user.Username}</span>");
        }

        public static string ParseEmojis(string text)
        {
            var parsedText = Emoji1Regex().Replace(text, match => $"<img src='{match.Groups[3]}' alt='{match.Groups[2]}' class='attachment-img' loading='lazy'>");
            if (parsedText == text)
            {
                parsedText = Emoji2Regex().Replace(text, match =>
                {
                    bool parseSuccess = ulong.TryParse(match.Groups[1].Value, out ulong emojiId);
                    return parsedText;
                });
            }

            return text;
        }


        public static bool ValidateMessage(DiscordMessage message)
        {
            if (message.Content == "" && message.Attachments.Count < 1 || message.MessageType != DiscordMessageType.Default) return false;
            return true;
        }

        public static string ConvertHexToRgba(string hexColor, float alpha)
        {
            hexColor = hexColor.Replace("#", string.Empty);

            byte r = byte.Parse(hexColor[..2], NumberStyles.HexNumber);
            byte g = byte.Parse(hexColor.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hexColor.Substring(4, 2), NumberStyles.HexNumber);

            return $"rgba({r}, {g}, {b}, {alpha.ToString().Replace(",", ".")})";
        }

        public static string ConvertBytes(long bytes)
        {
            if (bytes >= 1_073_741_824)
                return $"{bytes / 1_073_741_824.0:F2} GB";
            if (bytes >= 1_048_576)
                return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KB";

            return $"{bytes} bytes";
        }

        [GeneratedRegex("<(?!:[a-zA-Z0-9_]+:\\d{18}>)(?!@|#)(?!:)[^>]+?>")]
        private static partial Regex HtmlFilterRegex();

        [GeneratedRegex(@"\[(?<name>[^\]]+)\]\((?<url>https:\/\/cdn\.discordapp\.com\/emojis\/\d+\.(?<extension>webp|gif)\?size=\d+&quality=lossless&name=[^\)]+|https:\/\/media\.discordapp\.net\/stickers\/\d+\.(png|gif)\?size=\d+&name=[^\)]+)\)")]
        private static partial Regex Emoji1Regex();

        [GeneratedRegex(@"<:[a-zA-Z0-9_]+:(\d+)>")]
        private static partial Regex Emoji2Regex();
    }
}