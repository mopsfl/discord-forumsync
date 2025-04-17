using DSharpPlus;
using DSharpPlus.Entities;
using System.Text.RegularExpressions;
using dotenv.net;

namespace luaobfuscator_forumsync
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
            <link rel='stylesheet' href='/style.css'>
            <link rel='icon' type='image/x-icon' href='<!--guildIcon-->'>
        </head>
        <body>
        <div class='container'>
            <!--content-->
        </div>
        </body>
        </html>";
        private static readonly string htmlContent2 = @"<!DOCTYPE html>
        <html lang='en'>
        
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Forum Sync Test</title>
            <link rel='stylesheet' href='/style.css'>
            <link rel='icon' type='image/x-icon' href='<!--guildIcon-->'>
        </head>
        
        <body>
        <div class='container'>
            <!--content1-->
            <!--content2-->
        </div>
        </body>
        
        </html>";
        public static async Task Main()
        {
            DotEnv.Load();
            string? token = Environment.GetEnvironmentVariable("TOKEN");
            if (token == null) { Console.WriteLine("Token is null."); return; }

            DiscordClientBuilder clientBuilder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.All)
            .ConfigureEventHandlers(
                b => b.HandleMessageCreated(async (s, message) =>
                {
                    ForumSync.AddNewMessage(message);
                })
                .HandleMessageDeleted(async (e, message) =>
                {
                    ForumSync.RemovedDeletedMessage(message);
                })
                .HandleMessageUpdated(async (s, message) =>
                {
                    //Console.WriteLine("TODO: HandleMessageUpdated");
                })
                .HandleThreadCreated(async (s, thread) =>
                {
                    ForumSync.threadCache.Clear(); // TODO: dont clear lol
                })
            ).SetLogLevel(LogLevel.Debug);

            discordClient = clientBuilder.Build();
            await clientBuilder.ConnectAsync();

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.UseStaticFiles();
            app.MapGet("/", async () =>
            {
                string htmlStuff = $"<a href='/' class='path'>Servers</a>";
                List<DiscordGuild> guilds = await Utils.GetAllGuilds();

                foreach (var guild in guilds)
                {
                    htmlStuff += $@"<a class='threaditem'>
                        <img src='{guild.IconUrl ?? "/unknown.png"}' loading='lazy'>
                        <div>
                            <h3>{guild.Name}</h3>
                            <small class='gray'>{guild.Id}</small>
                        </div>
                        <!--{guild.Id}-->
                    </a>";

                    List<DiscordChannel> forumChannels = await Utils.GetAllGuildForums(guild);
                    string forumElements = "";

                    if (forumChannels != null)
                    {
                        foreach (var channel in forumChannels)
                        {
                            forumElements += $@"<a class='threaditem subitem' href='/forum/{channel.Id}'>
                                <span>{channel.Name}</span>
                            </a>";
                        }

                        htmlStuff = htmlStuff.Replace($@"<!--{guild.Id}-->", forumElements);
                    }
                    else htmlStuff = htmlStuff.Replace($@"<!--{guild.Id}-->", $@"<a class='threaditem subitem'>
                                <span>unable to load forums</span>
                            </a>");
                }

                string finalHtml = htmlContent1.Replace("<!--content-->", htmlStuff).Replace("<!--guildIcon-->", "");
                return Results.Content(finalHtml, "text/html");
            });
            app.MapGet("/forum", async () => { return Results.Redirect("/"); });
            app.MapGet("/forum/{channelId}", async (ulong channelId) =>
            {
                var data = await ForumSync.FetchForumData(channelId);
                if (data == null) return Results.NotFound("Channel not found.");
                if (discordClient == null) return Results.InternalServerError("internal error! discord client not set up.");
                DiscordChannel channel = await discordClient.GetChannelAsync(channelId);

                string htmlStuff = $@"<div style='display: flex; gap: 5px'>
                    <a href='/forum' class='path'>{channel.Guild.Name}</a>
                    <a href='/forum/{channelId}' class='path'>{channel.Name}</a>
                </div>";
                foreach (var thread in data)
                {
                    htmlStuff += $@"<a class='threaditem' href='/forum/{channelId}/{thread.Id}'>
                        <img src='{thread.AuthorAvatarUrl}' loading='lazy'>
                        <div>
                            <h3>{thread.Name}</h3>
                            <p><span class='inline-text'>{thread.AuthorName}</span> <span class='inline-text'>{thread.CreatedTimestampString}</span></p>
                        </div>
                    </a>";
                }

                string finalHtml = htmlContent1.Replace("<!--content-->", htmlStuff).Replace("<!--guildIcon-->", channel.Guild.IconUrl);
                return Results.Content(finalHtml, "text/html");
            });
            app.MapGet("/forum/{channelId}/{threadId}", async (ulong channelId, ulong threadId) =>
            {
                var data = await ForumSync.FetchForumData(channelId);
                if (data == null) return Results.NotFound("Channel not found.");
                DiscordChannel channel = await discordClient.GetChannelAsync(channelId);

                string htmlStuff = $@"<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.5.0/styles/monokai.min.css'><script src='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.5.0/highlight.min.js'></script><script>hljs.highlightAll();</script>";
                var thread = data.FirstOrDefault(t => t.Id == threadId);
                if (thread == null) return Results.NotFound("Thread not found.");

                foreach (var message in thread.Messages)
                {
                    if (message.Id == thread.FirstMessage?.Id || Utils.ValidateMessage(message) == false) continue;
                    string authorAvatarUrl = message.Author?.AvatarUrl ?? message.Author?.DefaultAvatarUrl ?? "";
                    string authorUsername = message.Author?.Username ?? "Deleted User";

                    htmlStuff += $@"<div class='threaditem grid' id='{message.Id}'>
                        <div class='threaditem-author'>
                            <img src='{authorAvatarUrl}'>
                            <div>
                                <p><span class='inline-text'>@{authorUsername}</span> <span class='inline-text'>{message.CreationTimestamp:HH:mm - dd/MM/yyyy}</span></p>
                            </div>
                        </div>
                        <span class='message-content'>{Utils.FormatMessageContent(message)}</span>
                    </div>";
                }

                string finalHtml = htmlContent2.Replace("<!--content1-->", $@"<div style='display: flex; gap: 5px'>
                    <a href='/forum' class='path'>{channel.Guild.Name}</a>
                    <a href='/forum/{channelId}' class='path'>{channel.Name}</a>
                    <a href='/forum/{channelId}/{thread.Id}' class='path'>{thread.Name}</a>
                </div>
                <a class='threaditem' style='margin-bottom: 10px'>
                    <img src='{thread.AuthorAvatarUrl}' loading='lazy'>
                    <div class='threaditem-author grid'>
                        <h3>{thread.Name}</h3>
                        <p><span class='inline-text'>@{thread.AuthorName}</span> <span class='inline-text'>{thread.CreatedTimestampString}</span></p>
                    </div>
                    <div class='break'></div>
                    <span class='message-content'>{Utils.FormatMessageContent(thread.FirstMessage)}</span>
                </a>").Replace("<!--content2-->", htmlStuff).Replace("<!--guildIcon-->", channel.Guild.IconUrl);

                return Results.Content(finalHtml, "text/html");
            });
            app.Run();

            await discordClient.ConnectAsync(new DiscordActivity("Forums", DiscordActivityType.Watching), DiscordUserStatus.Online);
            await Task.Delay(-1);
        }
    }
}