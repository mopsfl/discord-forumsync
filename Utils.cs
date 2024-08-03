using System.Diagnostics;
using System.Globalization;
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
            messageContent = ReplaceInlineCodeText(messageContent);
            messageContent = ReplaceEmojis(messageContent);
            messageContent = ReplaceBoldText(messageContent);
            messageContent = ReplaceCursiveText(messageContent);
            messageContent = ReplaceMarkdownUrl(messageContent);
            messageContent = messageContent.Replace("\n", "<br>");

            foreach (var attachment in message.Attachments)
            {
                if (attachment.ProxyUrl != null && attachment.Width != null)
                {
                    messageContent += $"<img src='{attachment.ProxyUrl}' class='attachment-img' loading='lazy'>";
                }
                else if (attachment.Width == null && attachment.FileName != null)
                {
                    messageContent += @$"<div class='attachment-file'>
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

            foreach (var sticker in message.Stickers)
            {
                if (sticker.StickerUrl != null) messageContent += $"<img src='{sticker.BannerUrl}' class='attachment-img' loading='lazy'>";
            }

            return messageContent;
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
    }
}