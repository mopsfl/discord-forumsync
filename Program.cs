using DSharpPlus;
using DSharpPlus.Entities;
using System.Text.RegularExpressions;
using dotenv.net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

// TODO: cache messages
//       add handler for message editing
//       fix buggy thread rendering (example: http://localhost:5046/forum/1207274943004282900/1284499655924908062)

namespace discord_forumsync
{
    public sealed class Program
    {
        public static readonly Regex urlRegex = new(
            @"^(https?:\/\/)?(www\.)?((\d{1,3}\.){3}\d{1,3}(:\d{1,5})?|([\w\-]+\.)+[a-zA-Z]{2,9})(\/.*)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline
        );
        public static DiscordClient? discordClient;
        private static readonly string htmlContent1 = @"<!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Forum Sync Test</title>
            <link rel='stylesheet' href='/font/ggans.css'>
            <link rel='stylesheet' href='/style.css'>
            <link rel='icon' type='image/x-icon' href='<!--guildIcon-->'>
            <link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.5.0/styles/monokai.min.css'>
            <link rel='stylesheet' href='https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0,0' />
            <script src='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.5.0/highlight.min.js'></script>
            <script>hljs.highlightAll();</script>
        </head>
        <body>
        <div class='container'>
            <!--content-->
        </div>
        </body>
        </html>";

        public static async Task Main()
        {
            DotEnv.Load();
            string? token = Environment.GetEnvironmentVariable("TOKEN");
            if (token == null)
            {
                Console.WriteLine("Please enter your bot token:");
                token = Console.ReadLine();
                if (token == null) return;
            }

            DiscordClientBuilder clientBuilder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.All)
            .ConfigureEventHandlers(
                b => b.HandleMessageCreated((s, message) =>
                {
                    Console.WriteLine(message);
                    return Task.CompletedTask;
                })
                .HandleMessageDeleted((e, message) =>
                {
                    Console.WriteLine(message);
                    return Task.CompletedTask;
                })
                .HandleMessageUpdated((s, message) =>
                {
                    //Console.WriteLine("TODO: HandleMessageUpdated");
                    return Task.CompletedTask;
                })
                .HandleThreadCreated((s, thread) =>
                {
                    ForumSync.threadCache.Clear(); // TODO: dont clear lol
                    return Task.CompletedTask;
                })
            ).SetLogLevel(LogLevel.Debug);

            discordClient = clientBuilder.Build();

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.UseStaticFiles();

            app.MapGet("/", async (HttpContext context) =>
            {
                string? syncId = context.Request.Cookies["__s"];
                if (syncId != null) context.Response.Cookies.Delete("__s");

                string htmlStuff = @$"
                <div style='display: flex; gap: 5px'><a href='/' class='path'>Servers</a></div>
                <div class='headerbuttons'>
                    <a class='headerbtn refresh' href='/forum/sync'><span class='material-symbols-outlined'>sync</span><span>Synchronize</span></a>
                    <a class='headerbtn addserver' href='/forum/add'><span class='material-symbols-outlined'>add</span><span>Add Server</span></a>
                </div>
                ";
                List<DiscordGuild>? guilds = await Utils.GetAllGuildsAsnyc(syncId);

                if (guilds == null) return Results.BadRequest("unable to get guilds");

                foreach (var guild in guilds)
                {
                    htmlStuff += $@"<a class='threaditem'>
                        <img src='{guild.IconUrl ?? "/unknown.png"}' loading='lazy'>
                        <div class='guild'>
                            <span class='guildname'>{guild.Name}</span>
                            <small class='gray guildid'>{guild.Id}</small>
                        </div>
                        <!--{guild.Id}-->
                    </a>";

                    List<DiscordForumChannel> forumChannels = await Utils.GetAllGuildForums(guild, syncId);
                    string forumElements = "";

                    if (forumChannels != null)
                    {
                        foreach (var channel in forumChannels)
                        {
                            forumElements += $@"<a class='threaditem subitem' href='/forum/{guild.Id}/{channel.Id}'><span class='forumname'><span class='channelhashtag'>#</span>{channel.Name}</span></a>";
                        }

                        htmlStuff = htmlStuff.Replace($@"<!--{guild.Id}-->", forumElements);
                    }
                    else htmlStuff = htmlStuff.Replace($@"<!--{guild.Id}-->", $@"<a class='threaditem subitem'><span>unable to load forums</span></a>");
                }

                string finalHtml = htmlContent1.Replace("<!--content-->", htmlStuff).Replace("<!--guildIcon-->", "");
                return Results.Content(finalHtml, "text/html");
            });
            app.MapGet("/forum", (HttpContext context) =>
            {
                var sync = context.Request.Query["sync"].ToString() ?? "0";
                if (sync == "1")
                {
                    var syncId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

                    context.Response.Cookies.Append("__s", syncId);
                    return Results.Redirect($"/");
                }
                return Results.Redirect("/");
            });
            app.MapGet("/forum/add", () =>
            {

                return Results.Redirect("https://discord.com/oauth2/authorize?client_id=1268568114744786954&permissions=66560&integration_type=0&scope=bot");
            });
            app.MapGet("/forum/sync", (HttpContext context) =>
            {
                // TODO
                return Results.Redirect("./?sync=1");
            });

            app.MapGet("/forum/{guildId}", (ulong guildId) => { return Results.Redirect("/"); });
            app.MapGet("/forum/{guildId}/{channelId}", async (ulong guildId, ulong channelId, int count = 10) =>
            {
                DiscordGuild? guild = await Utils.GetGuildAsync(guildId);
                DiscordChannel? channel = await Utils.GetChannelAsync(channelId);

                if (channel?.Type != DiscordChannelType.GuildForum) return Results.BadRequest("<channel> is not a <GuildForum>");

                List<DiscordThreadChannel>? threads = await Utils.GetThreadsAsync(channel) ?? [];

                string guildName = guild?.Name ?? "N/A";
                string channelName = channel?.Name ?? "N/A";

                string htmlStuff = $@"<div style='display: flex; gap: 5px'>
                    <a href='/forum' class='path'>{guildName}</a>
                    <a href='/forum/{guildId}/{channelId}' class='path'>{channelName}</a>
                </div>";

                foreach (var thread in threads[..Math.Min(count, threads.Count)])
                {
                    DiscordUser? author = await Utils.GetUserAsync(thread.CreatorId);

                    htmlStuff += $@"<a class='threaditem' href='/forum/{guildId}/{channelId}/{thread.Id}'>
                        <img src='{author?.AvatarUrl}' loading='lazy'>
                        <div class='thread'>
                            <span class='threadname'>{thread.Name}</span>
                            <p><span class='inline-text'>@{author?.Username}</span> <span class='inline-text'>{thread.CreationTimestamp:HH:mm - dd/MM/yyyy}</span></p>
                        </div>
                    </a>";
                }

                if (count < threads.Count) htmlStuff += $@"<a class='viewmore' href='?count={count + 5}'>view more</a>";

                string finalHtml = htmlContent1.Replace("<!--content-->", htmlStuff).Replace("<!--guildIcon-->", "");
                return Results.Content(finalHtml, "text/html");
            });
            app.MapGet("/forum/{guildId}/{channelId}/{threadId}", async (ulong guildId, ulong channelId, ulong threadId, int count = 10) =>
            {
                DiscordGuild? guild = await Utils.GetGuildAsync(guildId);
                DiscordChannel? channel = await Utils.GetChannelAsync(channelId);
                DiscordChannel? _thread = await Utils.GetChannelAsync(threadId);

                if (channel?.Type != DiscordChannelType.GuildForum) return Results.BadRequest("<channel> is not a <GuildForum>");
                if (_thread?.Type != DiscordChannelType.PublicThread) return Results.BadRequest("<thread> is not a <PublicThread>");

                DiscordThreadChannel thread = (DiscordThreadChannel)_thread;
                List<DiscordMessage> threadMessages = [];

                await foreach (var message in _thread.GetMessagesAsync()) threadMessages.Add(message);

                string guildName = guild?.Name ?? "N/A";
                string channelName = channel?.Name ?? "N/A";
                DiscordUser? author = await Utils.GetUserAsync(thread.CreatorId);

                string htmlStuff = $@"<div style='display: flex; gap: 5px'>
                    <a href='/forum' class='path'>{guildName}</a>
                    <a href='/forum/{guildId}/{channelId}' class='path'>{channelName}</a>
                    <a href='/forum/{guildId}/{channelId}/{threadId}' class='path'>{thread.Name}</a>
                </div>";


                htmlStuff += $@"<div class='threaditem firstmsg'>
                    <img src='{author?.AvatarUrl}' loading='lazy'>
                    <div class='thread'>
                        <span class='threadname'>{thread.Name}</span>
                        <p><span class='inline-text'>@{author?.Username}</span> <span class='inline-text'>{thread.CreationTimestamp:HH:mm - dd/MM/yyyy}</span></p>
                    </div>
                    <div class='break'></div>
                    <span class='message-content'>{Utils.FormatMessageContent(threadMessages.Last(), guild)}</span>
                </div>";

                foreach (var message in threadMessages)
                {
                    if (Utils.ValidateMessage(message) == false) continue;
                    if (message.Id != threadMessages.Last()?.Id)
                    {
                        string authorAvatarUrl = message.Author?.AvatarUrl ?? message.Author?.DefaultAvatarUrl ?? "";
                        string authorUsername = message.Author?.Username ?? "Deleted User";

                        htmlStuff += $@"<div class='threaditem grid' id='{message.Id}'>
                            <div class='thread-author'>
                                <img src='{authorAvatarUrl}'>
                                <div class='thread-author-info'>
                                    <span class='inline-text'>@{authorUsername}</span>
                                    <span class='inline-text'>{message.CreationTimestamp:HH:mm - dd/MM/yyyy}</span>
                                </div>
                            </div>
                            <span class='message-content'>{Utils.FormatMessageContent(message, guild)}</span>
                        </div>";
                    }
                }

                string finalHtml = htmlContent1.Replace("<!--content-->", htmlStuff).Replace("<!--guildIcon-->", "");
                return Results.Content(finalHtml, "text/html");
            });

            app.Run();
            await clientBuilder.ConnectAsync();
            await discordClient.UpdateStatusAsync(new DiscordActivity("Forums", DiscordActivityType.Watching), DiscordUserStatus.Online);
            await Task.Delay(-1);
        }
    }
}