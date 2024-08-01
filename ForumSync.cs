using System.Diagnostics;
using System.Runtime.Caching;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace luaobfuscator_forumsync
{
    public class ForumSync
    {
        private static readonly Dictionary<ulong, List<DiscordMessage>> messageCache = [];
        public async static Task<List<ForumThread>> FetchForumData(ulong channelId)
        {
            try
            {
                if (Program.discordClient == null) return [];

                var threads = new List<ForumThread>();
                var channel = await Program.discordClient.GetChannelAsync(channelId);
                if (channel.Type != ChannelType.GuildForum) return [];

                foreach (var forumThread in channel.Threads)
                {
                    // cache all the messages somewhere where you want. ill just do it like this for concept
                    List<DiscordMessage> threadMessages = [];
                    if (!messageCache.TryGetValue(forumThread.Id, out var cachedMessages))
                    {
                        Console.WriteLine($"Fetching messages from thread id {forumThread.Id}");
                        await FetchAndCacheMessagesAsync(forumThread.Id);
                    }
                    else threadMessages = cachedMessages;

                    // get the first message of the thread. so the thread body thing ahhh yk ig
                    DiscordMessage firstMessage;
                    if (threadMessages.Count > 0)
                    {
                        firstMessage = threadMessages.Last();
                    }
                    else // for some reason it doesnt fetch all messages first. maybe im just dumb or idk.
                    {
                        await FetchAndCacheMessagesAsync(forumThread.Id);
                        messageCache.TryGetValue(forumThread.Id, out var cachedMessages2);
                        if (cachedMessages2 == null) continue;

                        threadMessages = cachedMessages2;
                        firstMessage = threadMessages.Last();
                    };

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
                return [];
            }
        }

        public static void AddNewMessage(MessageCreateEventArgs eventArgs)
        {
            if (eventArgs.Channel.Parent.Type != ChannelType.GuildForum) { Debug.WriteLine("ChannelType is not a GuildForum"); return; };
            if (messageCache[eventArgs.Channel.Id] == null) { Debug.WriteLine("ChannelId not found in messageCache"); return; };

            Console.WriteLine($"New message sent in thread {eventArgs.Channel.Id} | {eventArgs.Message.Id}");
            messageCache[eventArgs.Channel.Id].Insert(0, eventArgs.Message);

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