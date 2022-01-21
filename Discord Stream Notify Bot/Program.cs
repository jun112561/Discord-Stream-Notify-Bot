﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_Stream_Notify_Bot.Command;
using Discord_Stream_Notify_Bot.DataBase.Table;
using Discord_Stream_Notify_Bot.Interaction;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Discord_Stream_Notify_Bot
{
    class Program
    {
        public static string VERSION => GetLinkerTime(Assembly.GetEntryAssembly());
        public static ConnectionMultiplexer Redis { get; set; }
        public static ISubscriber RedisSub { get; set; }
        public static IDatabase RedisDb { get; set; }

        public static IUser ApplicatonOwner { get; private set; } = null;
        public static DiscordSocketClient _client;
        public static BotPlayingStatus Status = BotPlayingStatus.Guild;
        public static Stopwatch stopWatch = new Stopwatch();
        public static bool isConnect = false, isDisconnect = false;
        public static bool isHoloChannelSpider = false, isNijisanjiChannelSpider = false, isOtherChannelSpider = false;
        static Timer timerUpdateStatus;
        static BotConfig botConfig = new();

        public enum BotPlayingStatus { Guild, Member, Stream, StreamCount, Info }

        static void Main(string[] args)
        {
            stopWatch.Start();

            Log.Info(VERSION + " 初始化中");
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CancelKeyPress += Console_CancelKeyPress;

            botConfig.InitBotConfig();
            timerUpdateStatus = new Timer(TimerHandler);

            if (!Directory.Exists(Path.GetDirectoryName(GetDataFilePath(""))))
                Directory.CreateDirectory(Path.GetDirectoryName(GetDataFilePath("")));

            using (var db = DataBase.DBContext.GetDbContext())
            {
                db.Database.EnsureCreated();
            }

            try
            {
                RedisConnection.Init(botConfig.RedisOption);
                Redis = RedisConnection.Instance.ConnectionMultiplexer;
                RedisSub = Redis.GetSubscriber();
                RedisDb = Redis.GetDatabase();

                Log.Info("Redis已連線");

                if (RedisSub.Publish("youtube.test", "nope") != 0)
                {
                    Log.Info("Redis Sub已存在");
                }
                else
                {
                    Log.Warn("Redis Sub不存在，請開啟錄影工具");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Redis連線錯誤，請確認伺服器是否已開啟");
                Log.Error(ex.Message);
                return;
            }
           
            new Program().MainAsync().GetAwaiter().GetResult();

            Redis.GetSubscriber().UnsubscribeAll();
        }

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
#if DEBUG
                LogLevel = LogSeverity.Verbose,
#else
                LogLevel = LogSeverity.Critical,
#endif
                ConnectionTimeout = int.MaxValue,
                MessageCacheSize = 50,
                GatewayIntents = GatewayIntents.AllUnprivileged
            }); ;

            #region 初始化一般指令系統
            var commandServices = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(botConfig)
                .AddSingleton(new CommandService(new CommandServiceConfig()
                {
                    CaseSensitiveCommands = false,
                    DefaultRunMode = Discord.Commands.RunMode.Async
                }));

            commandServices.LoadCommandFrom(Assembly.GetAssembly(typeof(CommandHandler)));
            IServiceProvider service = commandServices.BuildServiceProvider();
            await service.GetService<CommandHandler>().InitializeAsync();
            #endregion

            #region 初始化互動指令系統
            var interactionServices = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(botConfig)
                .AddSingleton(new InteractionService(_client, new InteractionServiceConfig()
                {
                    AutoServiceScopes = true,
                    UseCompiledLambda = true,
                    EnableAutocompleteHandlers = true,
                    DefaultRunMode = Discord.Interactions.RunMode.Async
                })); ;

            interactionServices.LoadInteractionFrom(Assembly.GetAssembly(typeof(InteractionHandler)));
            IServiceProvider iService = interactionServices.BuildServiceProvider();
            await iService.GetService<InteractionHandler>().InitializeAsync();
            #endregion

            #region 初始化Discord設定與事件
            _client.Log += Log.LogMsg;

            _client.Ready += async () =>
            {
                using (var db = DataBase.DBContext.GetDbContext())
                {
                    foreach (var guild in _client.Guilds)
                    {
                        if (!db.GuildConfig.Any(x => x.GuildId == guild.Id))
                        {
                            db.GuildConfig.Add(new GuildConfig() { GuildId = guild.Id });
                            db.SaveChanges();
                        }
                    }
                }

                stopWatch.Start();
                timerUpdateStatus.Change(0, 15 * 60 * 1000);

                ApplicatonOwner = (await _client.GetApplicationInfoAsync()).Owner;
                isConnect = true;

#if DEBUG
                if (botConfig.TestSlashCommandGuildId == 0 || _client.GetGuild(botConfig.TestSlashCommandGuildId) == null)
                    Log.Warn("未設定測試Slash伺服器或伺服器不存在，略過設定");
                else
                    await iService.GetService<InteractionService>().RegisterCommandsToGuildAsync(botConfig.TestSlashCommandGuildId);
#else
                await iService.GetService<InteractionService>().RegisterCommandsGloballyAsync();
#endif
            };

            _client.JoinedGuild += async (guild) =>
            {
                using (var db = DataBase.DBContext.GetDbContext())
                {
                    if (!db.GuildConfig.Any(x => x.GuildId == guild.Id))
                    {
                        db.GuildConfig.Add(new GuildConfig() { GuildId = guild.Id });
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                SendMessageToDiscord($"加入 {guild.Name}({guild.Id})\n擁有者: {guild.Owner.Username}({guild.Owner.Mention})");
            };
            #endregion

            await _client.LoginAsync(TokenType.Bot, botConfig.DiscordToken);
            await _client.StartAsync();

            Log.Info("已初始化完成!");

            do { await Task.Delay(1000); }
            while (!isDisconnect);

            while (isHoloChannelSpider || isNijisanjiChannelSpider || isOtherChannelSpider)
            {
                List<string> str = new List<string>();

                if (isHoloChannelSpider) str.Add("Holo");
                if (isNijisanjiChannelSpider) str.Add("Nijisanji");
                if (isOtherChannelSpider) str.Add("Other");

                Log.Info($"等待 {string.Join(", ", str)} 完成");
                await Task.Delay(5000);
            }

            await _client.StopAsync();
            await Command.Stream.Service.StreamService.SaveDateBase();
        }

        private static void TimerHandler(object state)
        {
            if (isDisconnect) return;

            ChangeStatus();
        }

        public static void ChangeStatus()
        {
            Action<string> setGame = new Action<string>((string text) => { _client.SetGameAsync($"s!h | {text}"); });

            switch (Status)
            {
                case BotPlayingStatus.Guild:
                    setGame($"在 {_client.Guilds.Count} 個伺服器");
                    Status = BotPlayingStatus.Member;
                    break;
                case BotPlayingStatus.Member:
                    try
                    {
                        setGame($"服務 {_client.Guilds.Sum((x) => x.MemberCount)} 個成員");
                        Status = BotPlayingStatus.Info;
                    }
                    catch (Exception) { Status = BotPlayingStatus.Stream; ChangeStatus(); }
                    break;
                case BotPlayingStatus.Stream:
                    Status = BotPlayingStatus.StreamCount;
                    try
                    {
                        using (var db = DataBase.DBContext.GetDbContext())
                        {
                            List<StreamVideo> list = null;
                            switch (new Random().Next(0, 2))
                            {
                                case 0:
                                    list = Queryable.Select(db.HoloStreamVideo, (x) => (StreamVideo)x).ToList();
                                    break;
                                case 1:
                                    list = Queryable.Select(db.NijisanjiStreamVideo, (x) => (StreamVideo)x).ToList();
                                    break;
                                case 2:
                                    list = Queryable.Select(db.OtherStreamVideo, (x) => (StreamVideo)x).ToList();
                                    break;
                            }
                            var item = list[new Random().Next(0, list.Count)];
                            setGame($"{item.VideoId} - {item.VideoTitle}\n{item.ChannelTitle}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ChangeStatus");
                        Log.Error(ex.Message);
                        ChangeStatus();
                    }
                    break;
                case BotPlayingStatus.StreamCount:
                    Status = BotPlayingStatus.Info;
                    setGame($"看了 {Utility.GetDbStreamCount()} 個直播");
                    break;
                case BotPlayingStatus.Info:
                    setGame("去看你的直播啦");
                    Status = BotPlayingStatus.Guild;
                    break;
            }
        }

        public static string GetDataFilePath(string fileName)
        {
            return AppDomain.CurrentDomain.BaseDirectory + "Data" +
                (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\\" : "/") + fileName;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            isDisconnect = true;
            e.Cancel = true;
        }

        public static string GetLinkerTime(Assembly assembly)
        {
            const string BuildVersionMetadataPrefix = "+build";

            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value[(index + BuildVersionMetadataPrefix.Length)..];
                    return value;
                }
            }
            return default;
        }

        public static void SendMessageToDiscord(string content)
        {
            Message message = new Message();

            if (isConnect) message.username = _client.CurrentUser.Username;
            else message.username = "Bot";

            if (isConnect) message.avatar_url = _client.CurrentUser.GetAvatarUrl();
            else message.avatar_url = "";

            message.content = content;

            using (WebClient webClient = new WebClient())
            {
                webClient.Encoding = System.Text.Encoding.UTF8;
                webClient.Headers["Content-Type"] = "application/json";
                webClient.UploadString(botConfig.WebHookUrl, JsonConvert.SerializeObject(message));
            }
        }

        public class Message
        {
            public string username { get; set; }
            public string content { get; set; }
            public string avatar_url { get; set; }
        }
    }
}
