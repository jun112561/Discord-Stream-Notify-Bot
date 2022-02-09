﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_Stream_Notify_Bot.DataBase.Table;
using Discord_Stream_Notify_Bot.Interaction.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Discord_Stream_Notify_Bot.Interaction.Youtube
{
    public partial class YoutubeStream : TopLevelModule<SharedService.Youtube.YoutubeStreamService>
    {
        private readonly DiscordSocketClient _client;
        private readonly HttpClients.DiscordWebhookClient _discordWebhookClient;

        public YoutubeStream(DiscordSocketClient client, HttpClients.DiscordWebhookClient discordWebhookClient)
        {
            _client = client;
            _discordWebhookClient = discordWebhookClient;
        }

        [SlashCommand("list-record-channel", "顯示直播記錄頻道")]
        public async Task ListRecordChannel()
        {
            using (var db = DataBase.DBContext.GetDbContext())
            {
                var nowRecordList = db.RecordYoutubeChannel.ToList().Select((x) => x.YoutubeChannelId).ToList();

                db.YoutubeChannelSpider.ToList().ForEach((item) => { if (item.IsWarningChannel && nowRecordList.Contains(item.ChannelId)) nowRecordList.Remove(item.ChannelId); });
                int warningChannelNum = db.YoutubeChannelSpider.Count((x) => x.IsWarningChannel);

                if (nowRecordList.Count > 0)
                {
                    var list = new List<string>();

                    for (int i = 0; i < nowRecordList.Count; i += 50)
                    {
                        list.AddRange(await GetChannelTitle(nowRecordList.Skip(i).Take(50)));
                    }

                    list.Sort();
                    await Context.SendPaginatedConfirmAsync(0, page =>
                    {
                        return new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle("直播記錄清單")
                            .WithDescription(string.Join('\n', list.Skip(page * 20).Take(20)))
                            .WithFooter($"{Math.Min(list.Count, (page + 1) * 20)} / {list.Count}個頻道 ({warningChannelNum}個隱藏的警告頻道)");
                    }, list.Count, 20, false);
                }
                else await Context.Interaction.SendConfirmAsync($"直播記錄清單中沒有任何頻道").ConfigureAwait(false);
            }
        }

        [SlashCommand("now-record-channel", "取得現在記錄直播的清單")]
        public async Task NowRecordChannel()
        {
            var newRecordStreamList = Discord_Stream_Notify_Bot.Utility.GetNowRecordStreamList();

            if (newRecordStreamList.Count == 0)
            {
                await Context.Interaction.SendConfirmAsync("現在沒有直播記錄").ConfigureAwait(false);
                return;
            }

            try
            {
                var yt = _service.yt.Videos.List("Snippet");
                yt.Id = string.Join(',', newRecordStreamList.Keys);
                var result = await yt.ExecuteAsync().ConfigureAwait(false);
                await Context.SendPaginatedConfirmAsync(0, (page) =>
                {
                    return new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("正在錄影的直播")
                        .WithDescription(string.Join("\n\n",
                            result.Items.Skip(page * 9).Take(9)
                            .Select((x) => $"{Format.Url(x.Snippet.Title, $"https://www.youtube.com/watch?v={x.Id}")}\n" +
                                $"{x.Snippet.ChannelTitle} - {newRecordStreamList[x.Id]}")))
                        .WithFooter($"{result.Items.Count}個頻道");
                }, result.Items.Count, 9, false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Context.Interaction.SendConfirmAsync(ex.Message).ConfigureAwait(false);
            }
        }

        [SlashCommand("now-streaming", "取得現在直播的Holo成員")]
        public async Task NowStreaming() //Todo: 加入2434
        {
            var embed = await _service.GetNowStreamingChannel().ConfigureAwait(false);

            if (embed == null) await Context.Interaction.SendConfirmAsync("無法取得直播清單").ConfigureAwait(false);
            else await Context.Interaction.RespondAsync(embed: embed).ConfigureAwait(false);
        }

        [SlashCommand("coming-soon-stream", "顯示接下來直播的清單")]
        public async Task ComingSoonStream()
        {
            try
            {
                List<Google.Apis.YouTube.v3.Data.Video> result = new List<Google.Apis.YouTube.v3.Data.Video>();

                for (int i = 0; i < _service.Reminders.Values.Count; i += 50)
                {
                    var yt = _service.yt.Videos.List("snippet,liveStreamingDetails");
                    yt.Id = string.Join(',', _service.Reminders.Values.Select((x) => x.StreamVideo.VideoId).Skip(i).Take(50));
                    result.AddRange((await yt.ExecuteAsync().ConfigureAwait(false)).Items);
                }
                using (var db = DataBase.DBContext.GetDbContext())
                {
                    result = result.OrderBy((x) => x.LiveStreamingDetails.ScheduledStartTime.Value).ToList();
                    await Context.SendPaginatedConfirmAsync(0, (act) =>
                    {
                        return new EmbedBuilder().WithOkColor()
                        .WithTitle("接下來開台的清單")
                        .WithDescription(string.Join("\n\n",
                           result.Skip(act * 7).Take(7)
                           .Select((x) => $"{Format.Url(x.Snippet.Title, $"https://www.youtube.com/watch?v={x.Id}")}" +
                           $"\n{Format.Url(x.Snippet.ChannelTitle, $"https://www.youtube.com/channel/{x.Snippet.ChannelId}")}" +
                           $"\n直播時間: {x.LiveStreamingDetails.ScheduledStartTime.Value}" +
                           "\n是否在直播錄影清單內: " + (db.RecordYoutubeChannel.Any((x2) => x2.YoutubeChannelId.Trim() == x.Snippet.ChannelId) ? "是" : "否"))));
                    }, result.Count, 7).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + "r\n" + ex.StackTrace);
                await Context.Interaction.SendErrorAsync("不明的錯誤，請向Bot擁有者回報", true);
            }
        }

        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageGuild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [CommandSummary("設定伺服器橫幅使用指定頻道的最新影片(直播)縮圖\r\n" +
            "若未輸入頻道Id則關閉本設定\r\n\r\n" +
            "Bot需要有管理伺服器權限\r\n" +
            "且伺服器需有Boost Lv2才可使用本設定\r\n" +
            "(此功能依賴直播通知，請確保設定的頻道在兩大箱或是爬蟲清單內)")]
        [SlashCommand("set-banner-change", "設定伺服器橫幅使用指定頻道的最新影片(直播)縮圖")]
        public async Task SetBannerChange([Summary("頻道Id")] string channelId)
        {
            using (var db = DataBase.DBContext.GetDbContext())
            {
                if (channelId == "")
                {
                    if (db.BannerChange.Any((x) => x.GuildId == Context.Guild.Id))
                    {
                        var guild = db.BannerChange.First((x) => x.GuildId == Context.Guild.Id);
                        db.BannerChange.Remove(guild);
                        await db.SaveChangesAsync();
                        await Context.Interaction.SendConfirmAsync("已移除橫幅設定");
                        return;
                    }
                    else
                    {
                        await Context.Interaction.SendConfirmAsync("伺服器並未使用本設定");
                        return;
                    }
                }
                else
                {
                    if (!channelId.Contains("UC"))
                    {
                        await Context.Interaction.SendConfirmAsync("頻道Id錯誤").ConfigureAwait(false);
                        return;
                    }

                    try
                    {
                        channelId = channelId.Substring(channelId.IndexOf("UC"), 24);
                    }
                    catch
                    {
                        await Context.Interaction.SendConfirmAsync("頻道Id格式錯誤，需為24字數").ConfigureAwait(false);
                        return;
                    }
                }

                if (Context.Guild.PremiumTier < PremiumTier.Tier2)
                {
                    await Context.Interaction.SendConfirmAsync("本伺服器未達Boost Lv2，不可設定橫幅\r\n" +
                        "故無法設定本功能");
                    return;
                }

                string channelTitle = await GetChannelTitle(channelId);

                if (channelTitle == "")
                {
                    await Context.Interaction.SendConfirmAsync($"頻道 {channelId} 不存在").ConfigureAwait(false);
                    return;
                }

                if (db.BannerChange.Any((x) => x.GuildId == Context.Guild.Id))
                {
                    var guild = db.BannerChange.First((x) => x.GuildId == Context.Guild.Id);
                    guild.ChannelId = channelId;
                    db.BannerChange.Update(guild);
                }
                else
                {
                    db.BannerChange.Add(new BannerChange() { GuildId = Context.Guild.Id, ChannelId = channelId });
                }

                await db.SaveChangesAsync();
                await Context.Interaction.SendConfirmAsync($"已設定伺服器橫幅使用 `{channelTitle}` 的直播縮圖").ConfigureAwait(false);
            }
        }

        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [CommandSummary("新增直播開台通知的頻道\r\n" +
            "頻道Id必須為24字數+UC開頭\r\n" +
            "或是完整的Youtube頻道網址\r\n" +
            "\r\n" +
            "輸入holo通知全部`Holo成員`的直播\r\n" +
            "輸入2434通知全部`彩虹社成員`的直播\r\n" +
            "(海外勢僅部分成員歸類在此選項內，建議改用`s!acs`設定)\r\n" +
            "輸入other通知部分`非兩大箱`的直播\r\n" +
            "(可以使用`s!lcs`查詢有哪些頻道)\r\n" +
            "輸入all通知全部`Holo + 2434 + 非兩大箱`的直播\r\n" +
            "(此選項會覆蓋所有的通知設定)")]
        [CommandExample("UCdn5BQ06XqgXoAxIhbqw5Rg", "all", "2434")]
        [SlashCommand("add-channel", "新增直播開台通知的頻道")]
        public async Task AddChannel([Summary("頻道Id")] string channelId, [Summary("發送通知的頻道")] ITextChannel textChannel)
        {
            try
            {
                channelId = channelId.GetChannelId();
            }
            catch (Exception ex)
            {
                await Context.Interaction.SendConfirmAsync(ex.Message).ConfigureAwait(false);
                return;
            }

            using (var db = DataBase.DBContext.GetDbContext())
            {
                if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId))
                {
                    await Context.Interaction.SendConfirmAsync($"{channelId} 已在直播通知清單內").ConfigureAwait(false);
                    return;
                }

                if (channelId == "all")
                {
                    bool followerup = false;
                    if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id))
                    {
                        if (await PromptUserConfirmAsync("直播通知清單已有需通知的頻道\r\n" +
                            $"是否更改為通知全部頻道的直播?\r\n" +
                            $"注意: 將會把原先設定的直播通知清單重置").ConfigureAwait(false))
                        {
                            db.NoticeYoutubeStreamChannel.RemoveRange(Queryable.Where(db.NoticeYoutubeStreamChannel, (x) => x.GuildId == Context.Guild.Id));
                            followerup = true;
                        }
                        else return;
                    }
                    db.NoticeYoutubeStreamChannel.Add(new NoticeYoutubeStreamChannel() { GuildId = Context.Guild.Id, DiscordChannelId = textChannel.Id, NoticeStreamChannelId = "all" });
                    await Context.Interaction.SendConfirmAsync($"將會通知全部的直播", followerup).ConfigureAwait(false);
                }
                else if (channelId == "holo" || channelId == "2434" || channelId == "other")
                {
                    if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == "all"))
                    {
                        if (await PromptUserConfirmAsync("已設定為通知全部頻道的直播\r\n" +
                            $"是否更改為僅通知 `{channelId}` 的直播?"))
                        {
                            db.NoticeYoutubeStreamChannel.Remove(db.NoticeYoutubeStreamChannel.First((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == "all"));
                            db.NoticeYoutubeStreamChannel.Add(new NoticeYoutubeStreamChannel() { GuildId = Context.Guild.Id, DiscordChannelId = textChannel.Id, NoticeStreamChannelId = channelId });
                            await Context.Interaction.SendConfirmAsync($"已將 {channelId} 加入到通知頻道清單內", true).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        db.NoticeYoutubeStreamChannel.Add(new NoticeYoutubeStreamChannel() { GuildId = Context.Guild.Id, DiscordChannelId = textChannel.Id, NoticeStreamChannelId = channelId });
                        await Context.Interaction.SendConfirmAsync($"已將 {channelId} 加入到通知頻道清單內").ConfigureAwait(false);
                    }
                }
                else
                {
                    string channelTitle = await GetChannelTitle(channelId).ConfigureAwait(false);
                    if (channelTitle == "")
                    {
                        await Context.Interaction.SendConfirmAsync($"頻道 {channelId} 不存在").ConfigureAwait(false);
                        return;
                    }

                    if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == "all"))
                    {
                        if (await PromptUserConfirmAsync("已設定為通知全部頻道的直播\r\n" +
                            $"是否更改為僅通知 `{channelTitle}` 的直播?"))
                        {
                            db.NoticeYoutubeStreamChannel.Remove(db.NoticeYoutubeStreamChannel.First((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == "all"));
                            db.NoticeYoutubeStreamChannel.Add(new NoticeYoutubeStreamChannel() { GuildId = Context.Guild.Id, DiscordChannelId = textChannel.Id, NoticeStreamChannelId = channelId });
                            await Context.Interaction.SendConfirmAsync($"已將 {channelTitle} 加入到通知頻道清單內", true).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        db.NoticeYoutubeStreamChannel.Add(new NoticeYoutubeStreamChannel() { GuildId = Context.Guild.Id, DiscordChannelId = textChannel.Id, NoticeStreamChannelId = channelId });
                        await Context.Interaction.SendConfirmAsync($"已將 {channelTitle} 加入到通知頻道清單內").ConfigureAwait(false);
                    }
                }

                await db.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [CommandSummary("移除直播開台通知的頻道\r\n" +
            "頻道Id必須為24字數+UC開頭\r\n" +
            "或是完整的Youtube頻道網址\r\n" +
            "\r\n" +
            "輸入holo移除全部 `Holo成員` 的直播通知\r\n" +
            "輸入2434移除全部 `彩虹社成員` 的直播通知\r\n" +
            "輸入other移除部分 `非兩大箱` 的直播通知\r\n" +
            "輸入all移除全部 `Holo + 2434 + 非兩大箱` 的直播通知")]
        [CommandExample("UCdn5BQ06XqgXoAxIhbqw5Rg", "all", "2434")]
        [SlashCommand("remove-channel", "移除直播開台通知的頻道")]
        public async Task RemoveChannel([Summary("頻道Id")] string channelId)
        {
            try
            {
                channelId = channelId.GetChannelId();
            }
            catch (Exception ex)
            {
                await Context.Interaction.SendConfirmAsync(ex.Message).ConfigureAwait(false);
                return;
            }
            using (var db = DataBase.DBContext.GetDbContext())
            {
                if (!db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id))
                {
                    await Context.Interaction.SendConfirmAsync("並未設定直播通知...").ConfigureAwait(false);
                    return;
                }

                if (channelId == "all")
                {
                    if (await PromptUserConfirmAsync("將移除全部的直播通知\r\n是否繼續?").ConfigureAwait(false))
                    {
                        db.NoticeYoutubeStreamChannel.RemoveRange(Queryable.Where(db.NoticeYoutubeStreamChannel, (x) => x.GuildId == Context.Guild.Id));
                        await Context.Interaction.SendConfirmAsync("已全部清除", true).ConfigureAwait(false);
                        await db.SaveChangesAsync();
                        return;
                    }
                    else return;
                }

                if (!db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId))
                {
                    await Context.Interaction.SendConfirmAsync($"並未設定`{channelId}`的直播通知...").ConfigureAwait(false);
                    return;
                }
                else
                {
                    if (channelId == "holo" || channelId == "2434" || channelId == "other")
                    {
                        db.NoticeYoutubeStreamChannel.Remove(db.NoticeYoutubeStreamChannel.First((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId));
                        await Context.Interaction.SendConfirmAsync($"已移除 {channelId}").ConfigureAwait(false);
                    }
                    else if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId))
                    {
                        string channelTitle = await GetChannelTitle(channelId).ConfigureAwait(false);
                        if (channelTitle == "")
                        {
                            await Context.Interaction.SendConfirmAsync($"頻道 {channelId} 不存在").ConfigureAwait(false);
                            return;
                        }

                        db.NoticeYoutubeStreamChannel.Remove(db.NoticeYoutubeStreamChannel.First((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId));
                        await Context.Interaction.SendConfirmAsync($"已移除 {channelTitle}").ConfigureAwait(false);
                    }

                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }

        [RequireContext(ContextType.Guild)]
        [SlashCommand("list-channel", "顯示現在已加入通知清單的直播頻道")]
        public async Task ListChannel([Summary("頁數")]int page = 0)
        {
            using (var db = DataBase.DBContext.GetDbContext())
            {
                var list = Queryable.Where(db.NoticeYoutubeStreamChannel, (x) => x.GuildId == Context.Guild.Id)
                .Select((x) => new KeyValuePair<string, ulong>(x.NoticeStreamChannelId, x.DiscordChannelId)).ToList();
                if (list.Count() == 0) { await Context.Interaction.SendConfirmAsync("直播通知清單為空").ConfigureAwait(false); return; }

                var ytChannelList = list.Select(x => x.Key).Where((x) => x.StartsWith("UC")).ToList();
                var channelTitleList = list.Where((x) => !x.Key.StartsWith("UC")).Select((x) => $"{x.Key} => <#{x.Value}>").ToList();

                if (ytChannelList.Count > 0)
                {
                    for (int i = 0; i < ytChannelList.Count; i += 50)
                    {
                        try
                        {
                            var channel = _service.yt.Channels.List("snippet");
                            channel.Id = string.Join(",", ytChannelList.Skip(i).Take(50));
                            var response = await channel.ExecuteAsync().ConfigureAwait(false);
                            channelTitleList.AddRange(response.Items.Select((x) => $"{x.Id} / {x.Snippet.Title} => <#{list.Find((x2) => x2.Key == x.Id).Value}>"));
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                        }
                    }
                }

                await Context.SendPaginatedConfirmAsync(page, page =>
                {
                    return new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("直播通知清單為空")
                        .WithDescription(string.Join('\n', channelTitleList.Skip(page * 20).Take(20)))
                        .WithFooter($"{Math.Min(channelTitleList.Count, (page + 1) * 20)} / {channelTitleList.Count}個頻道");
                }, channelTitleList.Count, 20, false);
            }
        }

        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages | GuildPermission.MentionEveryone)]
        [RequireBotPermission(GuildPermission.MentionEveryone)]
        [CommandSummary("設定通知訊息\r\n" +
            "不輸入通知訊息的話則會關閉該類型的通知\r\n" +
            "若輸入`-`則可以關閉該通知類型\r\n" +
            "需先新增直播通知後才可設定通知訊息(`s!h ansc`)\r\n\r\n" +
            "NoticeType(通知類型)說明:\r\n" +
            "NewStream: 新待機所\r\n" +
            "NewVideo: 新影片\r\n" +
            "Start: 開始直播\r\n" +
            "End: 結束直播\r\n" +
            "ChangeTime: 變更直播時間\r\n" +
            "Delete: 刪除直播\r\n\r\n" +
            "(考慮到有伺服器需Ping特定用戶組的情況，故Bot需提及所有身分組權限)\r\n" +
            "(建議在私人頻道中設定以免Ping到用戶組造成不必要的誤會)")]
        [CommandExample("UCXRlIK3Cw_TJIQC5kSJJQMg start @通知用的用戶組 阿床開台了",
            "holo newstream @某人 新待機所建立",
            "UCUKD-uaobj9jiqB-VXt71mA newstream -",
            "UCXRlIK3Cw_TJIQC5kSJJQMg end")]
        [SlashCommand("set-message", "設定通知訊息")]
        public async Task SetMessage([Summary("頻道Id")] string channelId, [Summary("通知類型")] SharedService.Youtube.YoutubeStreamService.NoticeType noticeType, [Summary("通知訊息")] string message = "")
        {
            try
            {
                channelId = channelId.GetChannelId();
            }
            catch (Exception ex)
            {
                await Context.Interaction.SendConfirmAsync(ex.Message).ConfigureAwait(false);
                return;
            }

            using (var db = DataBase.DBContext.GetDbContext())
            {
                if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId))
                {
                    var noticeStreamChannel = db.NoticeYoutubeStreamChannel.First((x) => x.GuildId == Context.Guild.Id && x.NoticeStreamChannelId == channelId);
                    string noticeTypeString = "";

                    message = message.Trim();
                    switch (noticeType)
                    {
                        case SharedService.Youtube.YoutubeStreamService.NoticeType.NewStream:
                            noticeStreamChannel.NewStreamMessage = message;
                            noticeTypeString = "新待機所";
                            break;
                        case SharedService.Youtube.YoutubeStreamService.NoticeType.NewVideo:
                            noticeStreamChannel.NewVideoMessage = message;
                            noticeTypeString = "新影片";
                            break;
                        case SharedService.Youtube.YoutubeStreamService.NoticeType.Start:
                            noticeStreamChannel.StratMessage = message;
                            noticeTypeString = "開始直播";
                            break;
                        case SharedService.Youtube.YoutubeStreamService.NoticeType.End:
                            noticeStreamChannel.EndMessage = message;
                            noticeTypeString = "結束直播";
                            break;
                        case SharedService.Youtube.YoutubeStreamService.NoticeType.ChangeTime:
                            noticeStreamChannel.ChangeTimeMessage = message;
                            noticeTypeString = "變更直播時間";
                            break;
                        case SharedService.Youtube.YoutubeStreamService.NoticeType.Delete:
                            noticeStreamChannel.DeleteMessage = message;
                            noticeTypeString = "刪除直播";
                            break;
                    }

                    db.NoticeYoutubeStreamChannel.Update(noticeStreamChannel);
                    await db.SaveChangesAsync();

                    if (message == "-") await Context.Interaction.SendConfirmAsync($"已關閉 {channelId} 的 {noticeTypeString} 通知").ConfigureAwait(false);
                    else if (message != "") await Context.Interaction.SendConfirmAsync($"已設定 {channelId} 的 {noticeTypeString} 通知訊息為:\r\n{message}").ConfigureAwait(false);
                    else await Context.Interaction.SendConfirmAsync($"已取消 {channelId} 的 {noticeTypeString} 通知訊息").ConfigureAwait(false);
                }
                else
                {
                    await Context.Interaction.SendConfirmAsync($"並未設定 {channelId} 的直播通知\r\n請先使用 `/youtube add-notice-channel {channelId}` 新增直播後再設定通知訊息").ConfigureAwait(false);
                }
            }
        }

        string GetCurrectMessage(string message)
            => message == "-" ? "(已關閉本類別的通知)" : message;

        [RequireContext(ContextType.Guild)]
        [SlashCommand("list-message", "列出已設定的通知訊息")]
        public async Task ListMessage([Summary("頁數")] int page = 0)
        {
            using (var db = DataBase.DBContext.GetDbContext())
            {
                if (db.NoticeYoutubeStreamChannel.Any((x) => x.GuildId == Context.Guild.Id))
                {
                    var noticeStreamChannels = db.NoticeYoutubeStreamChannel.ToList().Where((x) => x.GuildId == Context.Guild.Id);
                    Dictionary<string, string> dic = new Dictionary<string, string>();

                    foreach (var item in noticeStreamChannels)
                    {
                        var channelTitle = item.NoticeStreamChannelId;
                        if (channelTitle.StartsWith("UC")) channelTitle = (await GetChannelTitle(channelTitle).ConfigureAwait(false)) + $" ({item.NoticeStreamChannelId})";

                        dic.Add(channelTitle,
                            $"新待機所: {GetCurrectMessage(item.NewStreamMessage)}\r\n" +
                            $"新影片: {GetCurrectMessage(item.NewVideoMessage)}\r\n" +
                            $"開始直播: {GetCurrectMessage(item.StratMessage)}\r\n" +
                            $"結束直播: {GetCurrectMessage(item.EndMessage)}\r\n" +
                            $"變更直播時間: {GetCurrectMessage(item.ChangeTimeMessage)}\r\n" +
                            $"刪除直播: {GetCurrectMessage(item.DeleteMessage)}");
                    }

                    await Context.SendPaginatedConfirmAsync(page, (page) =>
                    {
                        EmbedBuilder embedBuilder = new EmbedBuilder().WithOkColor().WithTitle("通知訊息清單")
                            .WithDescription("如果沒訊息的話就代表沒設定\r\n不用擔心會Tag到用戶組，Embed不會有Ping的反應");

                        foreach (var item in dic.Skip(page * 4).Take(4))
                        {
                            embedBuilder.AddField(item.Key, item.Value);
                        }

                        return embedBuilder;
                    }, dic.Count, 4);
                }
                else
                {
                    await Context.Interaction.SendConfirmAsync($"並未設定直播通知\r\n請先使用 `/help get-command-help add-notice-channel` 查看說明並新增直播通知").ConfigureAwait(false);
                }
            }
        }
        private async Task<string> GetChannelTitle(string channelId)
        {
            try
            {
                var channel = _service.yt.Channels.List("snippet");
                channel.Id = channelId;
                var response = await channel.ExecuteAsync().ConfigureAwait(false);
                return response.Items[0].Snippet.Title;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                return "";
            }
        }

        private async Task<List<string>> GetChannelTitle(IEnumerable<string> channelId)
        {
            try
            {
                var channel = _service.yt.Channels.List("snippet");
                channel.Id = string.Join(",", channelId);
                var response = await channel.ExecuteAsync().ConfigureAwait(false);
                return response.Items.Select((x) => Format.Url(x.Snippet.Title, $"https://www.youtube.com/channel/{x.Id}")).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                return null;
            }
        }
    }
}