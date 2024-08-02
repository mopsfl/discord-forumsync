using System.Text.RegularExpressions;
using DSharpPlus.Entities;

namespace luaobfuscator_forumsync
{
    public class Utils
    {
        public static string FormatMessageContent(DiscordMessage message)
        {
            if (message == null) return "";
            string messageContent = message.Content;
            foreach (var user in message.MentionedUsers) messageContent = ReplaceMentionWithUsername(messageContent, user);
            messageContent = ReplaceCodeBlocks(messageContent);
            messageContent = ReplaceEmojis(messageContent);
            messageContent = messageContent.Replace("\n", "<br>");

            foreach (var attachment in message.Attachments)
            {
                if (attachment.ProxyUrl != null && attachment.Width != null)
                {
                    messageContent += $"<img src='{attachment.ProxyUrl}' class='attachment-img' loading='lazy'>";
                }
                else if (attachment.Width == null && attachment.FileName != null)
                {
                    messageContent += $"<a href='{attachment.Url}' class='attachment-file' target='_blank'>{attachment.FileName}</a>";
                }
            }

            foreach (var sticker in message.Stickers)
            {
                if (sticker.StickerUrl != null) messageContent += $"<img src='{sticker.BannerUrl}' class='attachment-img' loading='lazy'>";
            }

            return messageContent;
        }

        public static string ReplaceMentionWithUsername(string messageContent, DiscordUser user)
        {
            return messageContent.Replace($"<@{user.Id}>", $"<span class='mention'>@{user.Username}</span>", StringComparison.OrdinalIgnoreCase);
        }

        public static string ReplaceCodeBlocks(string content)
        {
            string pattern = @"\`+(\w+\n)?([\s\S]*?)\`+";
            return Regex.Replace(content, pattern, match =>
            {
                string codeContent = match.Groups[2].Value.Trim();
                return $"<span class='codeblock'>{codeContent}</span>";
            });
        }

        public static string ReplaceEmojis(string text)
        {
            string pattern = @"\[(?<name>[^\]]+)\]\((?<url>https:\/\/cdn\.discordapp\.com\/emojis\/\d+\.webp\?size=\d+&quality=lossless&name=[^\)]+|https:\/\/media\.discordapp\.net\/stickers\/\d+\.png\?size=\d+&name=[^\)]+)\)";
            string replacement = @"<img src='$2' alt='$1' class='attachment-img' loading='lazy'>";

            return Regex.Replace(text, pattern, replacement);
        }
    }
}