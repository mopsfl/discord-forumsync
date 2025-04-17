using System;
using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace luaobfuscator_forumsync
{
    public class ForumSync
    {
        public static readonly Dictionary<ulong, List<DiscordMessage>> messageCache = [];
        public static readonly Dictionary<ulong, List<DiscordThreadChannel>> threadCache = [];
        public async static Task<List<ForumThread>?> FetchForumData(ulong channelId)
        {
            try
            {
                if (Program.discordClient == null) return [];

                var threads = new List<ForumThread>();
                DiscordChannel channel = await Program.discordClient.GetChannelAsync(channelId);
                if (channel.Type != DiscordChannelType.GuildForum) return [];

                var fetchTasks = new List<Task>();
                var guild = await Program.discordClient.GetGuildAsync((ulong)channel.GuildId, false);

                var allThreads = await GetCachedThreadsAsync(channel);

                foreach (var forumThread in allThreads)
                {
                    List<DiscordMessage> threadMessages = [];
                    if (!messageCache.TryGetValue(forumThread.Id, out var cachedMessages))
                    {
                        Console.WriteLine($"Fetching messages from thread id {forumThread.Id}");
                        var fetchTask = FetchAndCacheMessagesAsync(forumThread.Id);
                        fetchTasks.Add(fetchTask);
                    }
                    else
                    {
                        threadMessages = cachedMessages;
                    }
                }

                await Task.WhenAll(fetchTasks);

                foreach (var forumThread in allThreads)
                {
                    if (!messageCache.TryGetValue(forumThread.Id, out var threadMessages))
                    {
                        Console.WriteLine($"Messages for thread id {forumThread.Id} were not fetched.");
                        continue;
                    }

                    DiscordMessage? firstMessage = threadMessages.Count > 0 ? threadMessages.Last() : null;
                    if (firstMessage == null)
                    {
                        await FetchAndCacheMessagesAsync(forumThread.Id);
                        messageCache.TryGetValue(forumThread.Id, out var cachedMessages2);
                        if (cachedMessages2 == null) continue;

                        threadMessages = cachedMessages2;
                        firstMessage = threadMessages.Last();
                    }

                    DiscordUser author = await Program.discordClient.GetUserAsync(forumThread.CreatorId);
                    threads.Add(new ForumThread(
                        name: forumThread.Name,
                        id: forumThread.Id,
                        ownerId: forumThread.CreatorId,
                        authorName: author.Username,
                        avatarUrl: author.AvatarUrl,
                        createdTimestamp: forumThread.CreationTimestamp.ToUnixTimeMilliseconds(),
                        createdTimestampString: forumThread.CreationTimestamp.ToString(@"HH:mm - dd/MM/yyyy"),
                        messages: threadMessages,
                        firstMessage: firstMessage
                    ));
                }
                threads = [.. threads.OrderByDescending(t => t.CreatedTimestamp)];
                return threads;
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
                return null;
            }
        }

        public async static Task<List<DiscordThreadChannel>> GetCachedThreadsAsync(DiscordChannel channel)
        {
            if (threadCache.TryGetValue(channel.Id, out var cachedThreads))
            {
                return cachedThreads;
            }

            var activeThreadResult = await channel.Guild.ListActiveThreadsAsync();
            var activeThreads = activeThreadResult.Threads
                .Where(thread => thread.ParentId == channel.Id)
                .ToList();

            var archivedResult = await channel.ListPublicArchivedThreadsAsync();
            var allThreads = activeThreads.Concat(archivedResult.Threads).ToList();

            threadCache[channel.Id] = allThreads;

            return allThreads;
        }


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
            ;
            if (messageCache[eventArgs.Channel.Id] == null) { Debug.WriteLine("ChannelId not found in messageCache"); return; }
            ;

            Console.WriteLine($"Deleted message in thread {eventArgs.Channel.Id} | {eventArgs.Message.Id}");
            messageCache[eventArgs.Channel.Id].RemoveAt(messageCache[eventArgs.Channel.Id].IndexOf(eventArgs.Message));

            return;
        }

        private static async Task FetchAndCacheMessagesAsync(ulong channelId)
        {
            if (Program.discordClient == null) return;

            var channel = await Program.discordClient.GetChannelAsync(channelId);
            if (channel != null)
            {
                var messages = new List<DiscordMessage>();

                await foreach (var message in channel.GetMessagesAsync())
                {
                    messages.Add(message);
                }

                messageCache[channelId] = messages;
            }
        }

    }

    public class ForumThread(string name, ulong id, ulong ownerId, long createdTimestamp, string createdTimestampString, string authorName, string avatarUrl, IReadOnlyList<DiscordMessage> messages, DiscordMessage firstMessage)
    {
        public string? Name = name;
        public DiscordMessage? FirstMessage = firstMessage;
        public ulong? Id = id;
        public ulong? OwnerId = ownerId;
        public long? CreatedTimestamp = createdTimestamp;
        public string? CreatedTimestampString = createdTimestampString;
        public IReadOnlyList<DiscordMessage> Messages = messages;
        public string AuthorName = authorName;
        public string AuthorAvatarUrl = avatarUrl;
    }
}