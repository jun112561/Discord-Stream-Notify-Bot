using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord_Stream_Notify_Bot.DataBase.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;

namespace Discord_Stream_Notify_Bot.Command
{
    static class Extensions
    {
        private static readonly IEmote arrow_left = new Emoji("⬅");
        private static readonly IEmote arrow_right = new Emoji("➡");

        public static EmbedBuilder WithOkColor(this EmbedBuilder eb) =>
           eb.WithColor(00, 229, 132);
        public static EmbedBuilder WithErrorColor(this EmbedBuilder eb) =>
           eb.WithColor(40, 40, 40);
        public static EmbedBuilder WithRecordColor(this EmbedBuilder eb) =>
           eb.WithColor(255, 0, 0);

        public static DateTime ConvertToDateTime(this string str) =>
           DateTime.Parse(str);

        public static string ConvertDateTimeToDiscordMarkdown(this DateTime dateTime)
        {
            long UTCTime = ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
            return $"<t:{UTCTime}:F> (<t:{UTCTime}:R>)";
        }

        public static StreamVideo.YTChannelType GetProductionType(this StreamVideo streamVideo)
        {
            using (var db = DataBase.DBContext.GetDbContext())
            {
                StreamVideo.YTChannelType type;
                var channel = db.YoutubeChannelOwnedType.FirstOrDefault((x) => x.ChannelId == streamVideo.ChannelId);

                if (channel != null)
                    type = channel.ChannelType;
                else
                    type = streamVideo.ChannelType;

                return type;
            }
        }

        public static string GetProductionName(this StreamVideo.YTChannelType channelType) =>        
                channelType == StreamVideo.YTChannelType.Holo ? "Hololive" : channelType == StreamVideo.YTChannelType.Nijisanji ? "彩虹社" : "其他";

        public static string GetCommandLine(this Process process)
        {
            if (!OperatingSystem.IsWindows()) return "";

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
                using (ManagementObjectCollection objects = searcher.Get())
                {
                    return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
                }
            }
            catch
            {
                return "";
            }
        }

        public static IEnumerable<T> Distinct<T, V>(this IEnumerable<T> source, Func<T, V> keySelector)
        {
            return source.Distinct(new CommonEqualityComparer<T, V>(keySelector));
        }

        public static bool HasStreamVideoByVideoId(this DataBase.DBContext dBContext, string videoId)
        {
            videoId = videoId.Trim();
            return dBContext.HoloStreamVideo.Any((x) => x.VideoId == videoId) ||
                dBContext.NijisanjiStreamVideo.Any((x) => x.VideoId == videoId) || 
                dBContext.OtherStreamVideo.Any((x) => x.VideoId == videoId);
        }

        public static StreamVideo GetStreamVideoByVideoId(this DataBase.DBContext dBContext, string videoId)
        {
            videoId = videoId.Trim();
            if (dBContext.HoloStreamVideo.Any((x) => x.VideoId == videoId))
            {
                return dBContext.HoloStreamVideo.First((x) => x.VideoId == videoId);
            }
            else if (dBContext.NijisanjiStreamVideo.Any((x) => x.VideoId == videoId))
            {
                return dBContext.NijisanjiStreamVideo.First((x) => x.VideoId == videoId);
            }
            else if (dBContext.OtherStreamVideo.Any((x) => x.VideoId == videoId))
            {
                return dBContext.OtherStreamVideo.First((x) => x.VideoId == videoId);
            }
            else
            {
                return null;
            }
        }

        public static StreamVideo GetLastStreamVideoByChannelId(this DataBase.DBContext dBContext, string channelId)
        {
            channelId = channelId.Trim();
            if (dBContext.HoloStreamVideo.Any((x) => x.ChannelId == channelId))
            {
                return dBContext.HoloStreamVideo.ToList().Last((x) => x.ChannelId == channelId);
            }
            else if (dBContext.NijisanjiStreamVideo.Any((x) => x.ChannelId == channelId))
            {
                return dBContext.NijisanjiStreamVideo.ToList().Last((x) => x.ChannelId == channelId);
            }
            else if (dBContext.OtherStreamVideo.Any((x) => x.ChannelId == channelId))
            {
                return dBContext.OtherStreamVideo.ToList().Last((x) => x.ChannelId == channelId);
            }
            else
            {
                return null;
            }
        }
        public static bool IsChannelInDb(this DataBase.DBContext dBContext, string channelId)
            => dBContext.HoloStreamVideo.Any((x) => x.ChannelId == channelId) ||
                dBContext.NijisanjiStreamVideo.Any((x) => x.ChannelId == channelId) ||
                dBContext.OtherStreamVideo.Any((x) => x.ChannelId == channelId);

        public static bool IsTwitterUserInDb(this DataBase.DBContext dBContext, string userId)
            => dBContext.TwitterSpaecSpider.Any((x) => x.UserId == userId);

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string des)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithOkColor().WithDescription(des).Build());
        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string title, string des)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithOkColor().WithTitle(title).WithDescription(des).Build());
        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string des)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithErrorColor().WithDescription(des).Build());
        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string title, string des)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithErrorColor().WithTitle(title).WithDescription(des).Build());

        static public async Task<bool> PromptUserConfirmAsync(this SocketCommandContext ctx, EmbedBuilder embed)
        {
            embed.WithOkColor()
                .WithFooter("yes/no");

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            try
            {
                var input = await GetUserInputAsync(ctx.Client, ctx.User.Id, ctx.Channel.Id).ConfigureAwait(false);
                input = input?.ToUpperInvariant();

                if (input != "YES" && input != "Y")
                {
                    return false;
                }

                return true;
            }
            finally
            {
                var _ = Task.Run(() => msg.DeleteAsync());
            }
        }

        static public async Task<string> GetUserInputAsync(DiscordSocketClient client, ulong userId, ulong channelId)
        {
            var userInputTask = new TaskCompletionSource<string>();
            try
            {
                client.MessageReceived += MessageReceived;

                if ((await Task.WhenAny(userInputTask.Task, Task.Delay(10000)).ConfigureAwait(false)) != userInputTask.Task)
                {
                    return null;
                }

                return await userInputTask.Task.ConfigureAwait(false);
            }
            finally
            {
                client.MessageReceived -= MessageReceived;
            }

            Task MessageReceived(SocketMessage arg)
            {
                var _ = Task.Run(() =>
                {
                    if (!(arg is SocketUserMessage userMsg) ||
                        !(userMsg.Channel is ITextChannel chan) ||
                        userMsg.Author.Id != userId ||
                        userMsg.Channel.Id != channelId)
                    {
                        return Task.CompletedTask;
                    }

                    if (userInputTask.TrySetResult(arg.Content))
                    {
                        userMsg.DeleteAfter(1);
                    }
                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            }
        }

        public static IMessage DeleteAfter(this IUserMessage msg, int seconds)
        {
            Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000).ConfigureAwait(false);
                try { await msg.DeleteAsync().ConfigureAwait(false); }
                catch { }
            });
            return msg;
        }

        public static IEnumerable<Type> LoadCommandFrom(this IServiceCollection collection, Assembly assembly)
        {
            List<Type> addedTypes = new List<Type>();

            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.Source);
                return Enumerable.Empty<Type>();
            }

            var services = new Queue<Type>(allTypes
                    .Where(x => x.GetInterfaces().Contains(typeof(ICommandService))
                        && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract)
                    .ToArray());

            addedTypes.AddRange(services);

            var interfaces = new HashSet<Type>(allTypes
                    .Where(x => x.GetInterfaces().Contains(typeof(ICommandService))
                        && x.GetTypeInfo().IsInterface));

            while (services.Count > 0)
            {
                var serviceType = services.Dequeue();

                if (collection.FirstOrDefault(x => x.ServiceType == serviceType) != null)
                    continue;
                                
                var interfaceType = interfaces.FirstOrDefault(x => serviceType.GetInterfaces().Contains(x));
                if (interfaceType != null)
                {
                    addedTypes.Add(interfaceType);
                    collection.AddSingleton(interfaceType, serviceType);
                }
                else
                {
                    collection.AddSingleton(serviceType, serviceType);
                }
            }

            return addedTypes;
        }

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel ch, EmbedBuilder embed, string msg = "")
            => ch.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions() { RetryMode = RetryMode.AlwaysRetry });

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel ch, string msg = "")
        {
            return ch.SendMessageAsync(null, false, new EmbedBuilder().WithOkColor().WithDescription(msg).Build(), new RequestOptions { RetryMode = RetryMode.AlwaysRetry });
        }

        public static Task SendPaginatedConfirmAsync(this ICommandContext ctx, int currentPage, Func<int, EmbedBuilder> pageFunc, int totalElements, int itemsPerPage, bool addPaginatedFooter = true)
            => ctx.SendPaginatedConfirmAsync(currentPage, (x) => Task.FromResult(pageFunc(x)), totalElements, itemsPerPage, addPaginatedFooter);

        public static async Task SendPaginatedConfirmAsync(this ICommandContext ctx, int currentPage,
    Func<int, Task<EmbedBuilder>> pageFunc, int totalElements, int itemsPerPage, bool addPaginatedFooter = true)
        {
            var embed = await pageFunc(currentPage).ConfigureAwait(false);

            var lastPage = (totalElements - 1) / itemsPerPage;

            if (addPaginatedFooter)
                embed.AddPaginatedFooter(currentPage, lastPage);

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false) as IUserMessage;

            if (lastPage == 0)
                return;

            await msg.AddReactionAsync(arrow_left).ConfigureAwait(false);
            await msg.AddReactionAsync(arrow_right).ConfigureAwait(false);

            await Task.Delay(2000).ConfigureAwait(false);

            var lastPageChange = DateTime.MinValue;

            async Task changePage(SocketReaction r)
            {
                try
                {
                    if (r.UserId != ctx.User.Id)
                        return;
                    if (DateTime.UtcNow - lastPageChange < TimeSpan.FromSeconds(1))
                        return;
                    if (r.Emote.Name == arrow_left.Name)
                    {
                        if (currentPage == 0)
                            return;
                        lastPageChange = DateTime.UtcNow;
                        var toSend = await pageFunc(--currentPage).ConfigureAwait(false);
                        if (addPaginatedFooter)
                            toSend.AddPaginatedFooter(currentPage, lastPage);
                        await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                    }
                    else if (r.Emote.Name == arrow_right.Name)
                    {
                        if (lastPage > currentPage)
                        {
                            lastPageChange = DateTime.UtcNow;
                            var toSend = await pageFunc(++currentPage).ConfigureAwait(false);
                            if (addPaginatedFooter)
                                toSend.AddPaginatedFooter(currentPage, lastPage);
                            await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("被取消了");
                    //ignored
                }
            }

            using (msg.OnReaction((DiscordSocketClient)ctx.Client, changePage, changePage))
            {
                await Task.Delay(30000).ConfigureAwait(false);
            }

            try
            {
                if (msg.Channel is ITextChannel && ((SocketGuild)ctx.Guild).CurrentUser.GuildPermissions.ManageMessages)
                {
                    await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
                else
                {
                    await Task.WhenAll(msg.Reactions.Where(x => x.Value.IsMe)
                        .Select(x => msg.RemoveReactionAsync(x.Key, ctx.Client.CurrentUser)));
                }
            }
            catch
            {
                // ignored
            }
        }

        public static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
        {
            if (lastPage != null)
                return embed.WithFooter(efb => efb.WithText($"{curPage + 1} / {lastPage + 1}"));
            else
                return embed.WithFooter(efb => efb.WithText(curPage.ToString()));
        }

        public static ReactionEventWrapper OnReaction(this IUserMessage msg, DiscordSocketClient client, Func<SocketReaction, Task> reactionAdded, Func<SocketReaction, Task> reactionRemoved = null)
        {
            if (reactionRemoved == null)
                reactionRemoved = _ => Task.CompletedTask;

            var wrap = new ReactionEventWrapper(client, msg);
            wrap.OnReactionAdded += (r) => { var _ = Task.Run(() => reactionAdded(r)); };
            wrap.OnReactionRemoved += (r) => { var _ = Task.Run(() => reactionRemoved(r)); };
            return wrap;
        }
    }
}
