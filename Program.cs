using System;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;

namespace luaobfuscator_forumsync
{
    public sealed class Program
    {
        public static DiscordClient? discordClient;
        private static readonly string htmlContent1 = @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Forum Sync Test</title>
    <style>
        * { color: white !important }
        body { background: #1c1c1c; }
        .container {
            margin: 1em 20%;
            display: grid;
            gap: 5px
        }
        .threaditem {
            background: #444;
            color: white;
            padding: 10px;
            font-family: Arial;
            text-decoration: none;
            display: flex;
            align-items: center;
        }
        img { width: 50px; height: 50px; margin-right: 10px }
        .threaditem > div > h3 { margin: 0; }
        .threaditem > div > p { margin: 0; margin-top: 10px }
        .threaditem > div > p > span {
            background: #2d2d2d;
            padding: 3px;
            border-radius: 3px;
        }
    </style>
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
            <style>
                body { background: #1c1c1c }

                .grid { display: grid !important }
        
                .container {
                    margin: 1em 20%;
                    display: grid;
                    gap: 5px
                }

                .threaditem {
                    background: #444;
                    color: white;
                    padding: 10px;
                    font-family: Arial;
                    text-decoration: none;
                    display: flex;
                    flex-wrap: wrap;
                    align-items: center;
                }

                .threaditem.grid > span {
                    margin-top: 10px
                }

                .break {
                    flex-basis: 100%;
                    height: 0;
                    margin-top: 20px;
                }

                img { width: 50px; height: 50px; margin-right: 10px }

                .threaditem > div > p, .threaditem > div > h3 { margin: 0 }
                .threaditem > div > h3 { margin: 0; }
                .threaditem > div > p { margin: 0; margin-top: 10px }
                .threaditem > div > p > span, 
                .threaditem-author > div > p > span{
                    background: #2d2d2d;
                    padding: 3px;
                    border-radius: 3px;
                }

                .threaditem-author {
                    display: flex;
                }
            </style>
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

            DiscordConfiguration config = new()
            {
                Token = "<DISCORD_BOT_TOKEN>",
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
            }; discordClient = new(config);

            DiscordActivity status = new("Forum", ActivityType.Watching);
            await discordClient.ConnectAsync(status, UserStatus.Online);

            // listen to messageCreate to update messageCache
            discordClient.MessageCreated += (client, eventArgs) =>
            {
                ForumSync.AddNewMessage(eventArgs);
                return Task.CompletedTask;
            };

            discordClient.MessageDeleted += (client, eventArgs) =>
            {
                ForumSync.RemovedDeletedMessage(eventArgs);
                return Task.CompletedTask;
            };

            // everything here is just for the concept obv.
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("/{channelId}", async (ulong channelId) =>
            {
                var data = await ForumSync.FetchForumData(channelId);
                if (data == null) return Results.NotFound("Channel not found.");

                string htmlStuff = "";
                foreach (var thread in data)
                {
                    htmlStuff += $@"<a class='threaditem' href='/{channelId}/{thread.Id}'>
                    <img src='{thread.AuthorAvatarUrl}'></img>
                    <div>
                        <h3>{thread.Name}</h3>
                        <p><span>{thread.AuthorName}</span> <span>{thread.CreatedTimestampString}</span></p>
                    </div>
                    </a>";
                }

                string finalHtml = htmlContent1.Replace("<!--content-->", htmlStuff);
                return Results.Content(finalHtml, "text/html");
            });

            app.MapGet("/{channelId}/{threadId}", async (ulong channelId, ulong threadId) =>
            {
                var data = await ForumSync.FetchForumData(channelId);
                if (data == null) return Results.NotFound("Channel not found.");

                string htmlStuff = "";
                var thread = data.FirstOrDefault(t => t.Id == threadId);
                if (thread == null) return Results.NotFound("Thread not found.");

                foreach (var message in thread.Messages)
                {
                    if (message.Id == thread.FirstMessage.Id) continue; // skip first message wich is the thread content yk
                    // check if not first message
                    htmlStuff += $@"<div class='threaditem grid'>
                        <div class='threaditem-author'>
                            <img src='{message.Author.AvatarUrl}'></img>
                            <div>
                                <p><span>{message.Author.Username}</span> <span>{message.CreationTimestamp:HH:mm - dd/MM/yyyy}</span></p>
                            </div>
                        </div>
                        <span>{message.Content}</span>
                    </div>";
                }

                string finalHtml = htmlContent2.Replace("<!--content1-->", $@"<a class='threaditem'>
                <img src='{thread.AuthorAvatarUrl}'></img>
                <div class='threaditem-author grid'>
                    <h3>{thread.Name}</h3>
                    <p><span>{thread.AuthorName}</span> <span>{thread.CreatedTimestampString}</span></p>
                </div>
                <div class='break'></div>
                <span>{thread.FirstMessage.Content}</span>
                </a>").Replace("<!--content2-->", htmlStuff);
                return Results.Content(finalHtml, "text/html");
            });

            app.Run();
            await Task.Delay(-1);
        }
    }
}