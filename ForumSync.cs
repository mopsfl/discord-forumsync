using System;
using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace discord_forumsync
{
    public class ForumSync
    {
        public static readonly Dictionary<ulong, List<DiscordMessage>> messageCache = [];
        public static readonly Dictionary<ulong, List<DiscordThreadChannel>> threadCache = [];

        public static void AddNewMessage(MessageCreatedEventArgs eventArgs)
        {
            if (eventArgs.Channel.Parent.Type != DiscordChannelType.GuildForum) { Debug.WriteLine("ChannelType is not a GuildForum"); return; }
            ;
            messageCache.TryGetValue(eventArgs.Channel.Id, out var messages);
            if (messages == null) return;

            Console.WriteLine($"New message sent in thread {eventArgs.Channel.Id} | {eventArgs.Message.Id}");
            messages.Insert(0, eventArgs.Message);

            return;
        }

        public static void RemovedDeletedMessage(MessageDeletedEventArgs eventArgs)
        {
            if (eventArgs.Channel.Parent.Type != DiscordChannelType.GuildForum) { Debug.WriteLine("ChannelType is not a GuildForum"); return; }

            if (messageCache[eventArgs.Channel.Id] == null) { Debug.WriteLine("ChannelId not found in messageCache"); return; }

            Console.WriteLine($"Deleted message in thread {eventArgs.Channel.Id} | {eventArgs.Message.Id}");
            messageCache[eventArgs.Channel.Id].RemoveAt(messageCache[eventArgs.Channel.Id].IndexOf(eventArgs.Message));

            return;
        }
    }
}