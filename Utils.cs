using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using FastCache;
using Markdig;

namespace luaobfuscator_forumsync
{
    public partial class Utils
    {
        public static string FormatMessageContent(DiscordMessage message)
        {
            if (message == null) return "";
            string messageContent = HtmlFilterRegex().Replace(message.Content, string.Empty);

            foreach (var user in message.MentionedUsers) messageContent = ReplaceMentionWithUsername(messageContent, user);
            messageContent = ReplaceEmojis(messageContent);
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

        public async static Task<List<DiscordChannel>> GetAllGuildForums(DiscordGuild guild)
        {
            if (Cached<List<DiscordChannel>>.TryGet(guild.Id, out var cachedChannels))
            {
                return cachedChannels;
            }

            var channels = await guild.GetChannelsAsync();
            List<DiscordChannel> forumChannels = [];

            foreach (var channel in channels)
            {
                if (channel.Type == DiscordChannelType.GuildForum)
                    forumChannels.Add(channel);
            }

            Cached<List<DiscordChannel>>.Save(guild.Id, forumChannels, TimeSpan.FromMinutes(60));
            return forumChannels;
        }

        public async static Task<List<DiscordGuild>> GetAllGuilds()
        {
            if (Program.discordClient == null) return [];
            if (Cached<List<DiscordGuild>>.TryGet("guilds", out var cachedGuilds))
            {
                return cachedGuilds;
            }

            List<DiscordGuild> guildList = [];

            await foreach (var guild in Program.discordClient.GetGuildsAsync())
            {
                guildList.Add(guild);
            }

            Cached<List<DiscordGuild>>.Save("guilds", guildList, TimeSpan.FromMinutes(60));

            return guildList;
        }


        public static string ReplaceMentionWithUsername(string messageContent, DiscordUser user)
        {
            return messageContent.Replace($"<@{user.Id}>", $"<span class='mention'>@{user.Username}</span>");
        }

        public static string ReplaceCodeBlocks(string content)
        {
            string pattern = @"\`\`\`(\w+\n)?([\s\S]*?)\`\`\`";
            return Regex.Replace(content, pattern, match =>
            {
                string codeContent = match.Groups[2].Value.Trim();
                return $"<span class='codeblock'>{codeContent}</span>";
            });
        }

        public static string ReplaceInlineCodeText(string content)
        {
            string pattern = @"`([^`]+?)`";
            return Regex.Replace(content, pattern, match =>
            {
                string codeContent = match.Groups[1].Value.Trim();
                return $"<span class='inline-text'>{codeContent}</span>";
            });
        }

        public static string ReplaceCursiveText(string content)
        {
            string pattern = @"_(?<text>[^_]+?)_|\*(?<text>[^*]+?)\*(?!\*)";
            return Regex.Replace(content, pattern, match =>
            {
                string textContent = match.Groups["text"].Value.Trim();
                return $"<span class='cursive-text'>{textContent}</span>";
            });
        }

        public static string ReplaceBoldText(string content)
        {
            string pattern = @"\*\*(?<text>[^*]+?)\*\*";
            return Regex.Replace(content, pattern, match =>
            {
                string textContent = match.Groups["text"].Value.Trim();
                return $"<span class='bold-text'>{textContent}</span>";
            });
        }

        public static string ReplaceMarkdownUrl(string content)
        {
            string pattern = @"\[(?<name>\w+)\]\((?<url>https\:\/\/.*)\)";
            return Regex.Replace(content, pattern, match =>
            {
                string url = match.Groups["url"].Value.Trim();
                string name = match.Groups["name"].Value.Trim();
                return $"<a href='{url}' target='_blank'>{name}</a>";
            });
        }

        public static string ReplaceEmojis(string text)
        {
            string pattern = @"\[(?<name>[^\]]+)\]\((?<url>https:\/\/cdn\.discordapp\.com\/emojis\/\d+\.(?<extension>webp|gif)\?size=\d+&quality=lossless&name=[^\)]+|https:\/\/media\.discordapp\.net\/stickers\/\d+\.(png|gif)\?size=\d+&name=[^\)]+)\)";

            return Regex.Replace(text, pattern, match =>
            {
                return $"<img src='{match.Groups[3]}' alt='{match.Groups[2]}' class='attachment-img' loading='lazy'>";
            });
        }

        public static bool ValidateMessage(DiscordMessage message)
        {
            if (message.Content == "" && message.Attachments.Count < 1) return false;
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

        [GeneratedRegex("<(?!@|#)[^>]+?>")]
        private static partial Regex HtmlFilterRegex();
    }
}