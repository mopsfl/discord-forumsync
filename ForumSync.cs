using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace luaobfuscator_forumsync
{
    public class ForumSync
    {
        public static readonly Dictionary<ulong, List<DiscordMessage>> messageCache = [];
        public async static Task<List<ForumThread>?> FetchForumData(ulong channelId)
        {
            try
            {
                if (Program.discordClient == null) return [];

                var threads = new List<ForumThread>();
                var channel = await Program.discordClient.GetChannelAsync(channelId);
                if (channel.Type != ChannelType.GuildForum) return [];

                var fetchTasks = new List<Task>();

                foreach (var forumThread in channel.Threads)
                {
                    // cache all the messages somewhere where you want. ill just do it like this for concept
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

                foreach (var forumThread in channel.Threads)
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

        public static void AddNewMessage(MessageCreateEventArgs eventArgs)
        {
            if (eventArgs.Channel.Parent.Type != ChannelType.GuildForum) { Debug.WriteLine("ChannelType is not a GuildForum"); return; };
            messageCache.TryGetValue(eventArgs.Channel.Id, out var messages);
            if (messages == null) return;

            Console.WriteLine($"New message sent in thread {eventArgs.Channel.Id} | {eventArgs.Message.Id}");
            messages.Insert(0, eventArgs.Message);

            return;
        }

        public static void RemovedDeletedMessage(MessageDeleteEventArgs eventArgs)
        {
            if (eventArgs.Channel.Parent.Type != ChannelType.GuildForum) { Debug.WriteLine("ChannelType is not a GuildForum"); return; };
            if (messageCache[eventArgs.Channel.Id] == null) { Debug.WriteLine("ChannelId not found in messageCache"); return; };

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
                var messages = await channel.GetMessagesAsync();
                messageCache[channelId] = [.. messages];
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