﻿using Discord;
using Discord_Stream_Notify_Bot.DataBase.Table;
using Discord_Stream_Notify_Bot.Interaction;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Discord_Stream_Notify_Bot.SharedService.Youtube
{
    public partial class YoutubeStreamService
    {
        private void StartReminder(StreamVideo streamVideo, StreamVideo.YTChannelType channelType)
        {
            if (streamVideo.ScheduledStartTime > DateTime.Now.AddDays(7)) return;

            try
            {
                TimeSpan ts = streamVideo.ScheduledStartTime.AddMinutes(-1).Subtract(DateTime.Now);

                if (ts <= TimeSpan.Zero)
                {
                    ReminderTimerAction(streamVideo);
                }
                else
                {
                    var remT = new Timer(ReminderTimerAction, streamVideo, Math.Max(1000, (long)ts.TotalMilliseconds), Timeout.Infinite); //Todo: 修正直接開始直播時，無法直接執行ReminderTimerAction的問題

                    if (!Reminders.TryAdd(streamVideo, new ReminderItem() { StreamVideo = streamVideo, Timer = remT, ChannelType = channelType }))
                    {
                        remT.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(streamVideo.VideoTitle + " - " + streamVideo.ScheduledStartTime);
                Log.Error(ex.Message);
                throw;
            }
        }

        private async void ReminderTimerAction(object rObj)
        {
            var streamVideo = (StreamVideo)rObj;

            try
            {
                var videoResult = await GetVideoAsync(streamVideo.VideoId);

                if (videoResult == null)
                {
                    Log.Info($"{streamVideo.VideoId} 待機所被刪了");

                    EmbedBuilder embedBuilder = new EmbedBuilder();
                    embedBuilder.WithErrorColor()
                    .WithTitle(streamVideo.VideoTitle)
                    .WithDescription(Format.Url(streamVideo.ChannelTitle, $"https://www.youtube.com/channel/{streamVideo.ChannelId}"))
                    .WithUrl($"https://www.youtube.com/watch?v={streamVideo.VideoId}")
                    .AddField("直播狀態", "已刪除直播", true)
                    .AddField("排定開台時間", streamVideo.ScheduledStartTime.ConvertDateTimeToDiscordMarkdown(), true);

                    await SendStreamMessageAsync(streamVideo, embedBuilder.Build(), NoticeType.Delete).ConfigureAwait(false);
                    return;
                }

                DateTime startTime;
                if (videoResult.LiveStreamingDetails.ScheduledStartTime.HasValue) startTime = videoResult.LiveStreamingDetails.ScheduledStartTime.Value;
                else startTime = videoResult.LiveStreamingDetails.ActualStartTime.Value;

                using (var db = DataBase.DBContext.GetDbContext())
                {
                    if (startTime.AddMinutes(-2) < DateTime.Now)
                    {
                        bool isRecord = false;
                        streamVideo.VideoTitle = videoResult.Snippet.Title;

                        if (db.HasStreamVideoByVideoId(streamVideo.VideoId))
                        {
                            switch (streamVideo.ChannelType)
                            {
                                case StreamVideo.YTChannelType.Holo:
                                    try
                                    {
                                        var data = db.HoloStreamVideo.First((x) => x.VideoId == streamVideo.VideoId);
                                        data.VideoTitle = streamVideo.VideoTitle;
                                        db.HoloStreamVideo.Update(data);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                                        db.HoloStreamVideo.Update(streamVideo.ConvertToHoloStreamVideo());
                                    }
                                    break;
                                case StreamVideo.YTChannelType.Nijisanji:
                                    try
                                    {
                                        var data1 = db.NijisanjiStreamVideo.First((x) => x.VideoId == streamVideo.VideoId);
                                        data1.VideoTitle = streamVideo.VideoTitle;
                                        db.NijisanjiStreamVideo.Update(data1);
                                    }
                                    catch (Exception ex)
                                    {

                                        Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                                        db.NijisanjiStreamVideo.Update(streamVideo.ConvertToNijisanjiStreamVideo());
                                    }
                                    break;
                                case StreamVideo.YTChannelType.Other:
                                    try
                                    {
                                        var data1 = db.OtherStreamVideo.First((x) => x.VideoId == streamVideo.VideoId);
                                        data1.VideoTitle = streamVideo.VideoTitle;
                                        db.OtherStreamVideo.Update(data1);
                                    }
                                    catch (Exception ex)
                                    {

                                        Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                                        db.OtherStreamVideo.Update(streamVideo.ConvertToOtherStreamVideo());
                                    }
                                    break;
                            }

#if RELEASE
                            try
                            {
                                if (CanRecord(db, streamVideo))
                                {
                                    //Todo: 自定義化
                                    if (noticeRecordChannel == null) noticeRecordChannel = _client.GetGuild(738734668882640938).GetTextChannel(805134765191462942);

                                    if (Program.Redis != null)
                                    {
                                        if (Utility.GetNowRecordStreamList().ContainsKey(streamVideo.VideoId))
                                        {
                                            Log.Warn($"{streamVideo.VideoId} 已經在錄影了");
                                            return;
                                        }

                                        if (await Program.RedisSub.PublishAsync("youtube.record", streamVideo.VideoId) != 0)
                                        {
                                            Log.Info($"已發送錄影請求: {streamVideo.VideoId}");
                                            isRecord = true;

                                            await noticeRecordChannel.SendMessageAsync(embeds: new Embed[] { new EmbedBuilder().WithOkColor().WithDescription( $"{Format.Url(streamVideo.VideoTitle, $"https://www.youtube.com/watch?v={streamVideo.VideoId}")}\n" +
                                                $"{Format.Url(streamVideo.ChannelTitle, $"https://www.youtube.com/channel/{streamVideo.ChannelId}")}\n\n" +
                                                $"{$"youtube_{streamVideo.ChannelId}_{streamVideo.ScheduledStartTime:yyyyMMdd_HHmmss}_{streamVideo.VideoId}.ts"}").Build() });
                                        }
                                        else Log.Warn($"Redis Sub頻道不存在，請開啟錄影工具: {streamVideo.VideoId}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"ReminderTimerAction-Record {ex.Message}\r\n{ex.StackTrace}");
                            }
#endif

                            await ChangeGuildBannerAsync(streamVideo.ChannelId, streamVideo.VideoId);

                            if (!isRecord)
                            {
                                EmbedBuilder embedBuilder = new EmbedBuilder();
                                embedBuilder.WithTitle(streamVideo.VideoTitle)
                                .WithOkColor()
                                .WithDescription(Format.Url(streamVideo.ChannelTitle, $"https://www.youtube.com/channel/{streamVideo.ChannelId}"))
                                .WithImageUrl($"https://i.ytimg.com/vi/{streamVideo.VideoId}/maxresdefault.jpg")
                                .WithUrl($"https://www.youtube.com/watch?v={streamVideo.VideoId}")
                                .AddField("排定開台時間", streamVideo.ScheduledStartTime.ConvertDateTimeToDiscordMarkdown(), true)
                                .AddField("直播狀態", "開台中", true);
                                //.AddField("是否記錄直播", "否", true);

                                await SendStreamMessageAsync(streamVideo, embedBuilder.Build(), NoticeType.Start).ConfigureAwait(false);
                            }

                            if (Reminders.TryRemove(streamVideo, out var t))
                                t.Timer.Change(Timeout.Infinite, Timeout.Infinite);
                        }
                    }
                    else
                    {
                        Log.Info($"時間已更改 {streamVideo.ChannelTitle} - {streamVideo.VideoTitle}");

                        EmbedBuilder embedBuilder = new EmbedBuilder();
                        embedBuilder.WithErrorColor()
                        .WithTitle(streamVideo.VideoTitle)
                        .WithDescription(Format.Url(streamVideo.ChannelTitle, $"https://www.youtube.com/channel/{streamVideo.ChannelId}"))
                        .WithImageUrl($"https://i.ytimg.com/vi/{streamVideo.VideoId}/maxresdefault.jpg")
                        .WithUrl($"https://www.youtube.com/watch?v={streamVideo.VideoId}")
                        .AddField("直播狀態", "尚未開台(已更改時間)", true)
                        .AddField("排定開台時間", streamVideo.ScheduledStartTime.ConvertDateTimeToDiscordMarkdown(), true)
                        .AddField("更改開台時間", startTime.ConvertDateTimeToDiscordMarkdown(), true);

                        streamVideo.ScheduledStartTime = startTime;
                        switch (streamVideo.ChannelType)
                        {
                            case StreamVideo.YTChannelType.Holo:
                                try
                                {
                                    var data = db.HoloStreamVideo.First((x) => x.VideoId == streamVideo.VideoId);
                                    data.ScheduledStartTime = streamVideo.ScheduledStartTime;
                                    db.HoloStreamVideo.Update(data);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                                    db.HoloStreamVideo.Update(streamVideo.ConvertToHoloStreamVideo());
                                }
                                break;
                            case StreamVideo.YTChannelType.Nijisanji:
                                try
                                {
                                    var data1 = db.NijisanjiStreamVideo.First((x) => x.VideoId == streamVideo.VideoId);
                                    data1.ScheduledStartTime = streamVideo.ScheduledStartTime;
                                    db.NijisanjiStreamVideo.Update(data1);
                                }
                                catch (Exception ex)
                                {

                                    Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                                    db.NijisanjiStreamVideo.Update(streamVideo.ConvertToNijisanjiStreamVideo());
                                }
                                break;
                            case StreamVideo.YTChannelType.Other:
                                try
                                {
                                    var data1 = db.OtherStreamVideo.First((x) => x.VideoId == streamVideo.VideoId);
                                    data1.ScheduledStartTime = streamVideo.ScheduledStartTime;
                                    db.OtherStreamVideo.Update(data1);
                                }
                                catch (Exception ex)
                                {

                                    Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                                    db.OtherStreamVideo.Update(streamVideo.ConvertToOtherStreamVideo());
                                }
                                break;
                        }

                        await SendStreamMessageAsync(streamVideo, embedBuilder.Build(), NoticeType.ChangeTime).ConfigureAwait(false);

                        if (Reminders.TryRemove(streamVideo, out var t))
                            t.Timer.Change(Timeout.Infinite, Timeout.Infinite);

                        StartReminder(streamVideo, streamVideo.ChannelType);
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex) { Log.Error($"ReminderAction {ex.Message} ({streamVideo.VideoId})\r\n{ex.StackTrace}"); }
        }

        private async Task SendStreamMessageAsync(string videolId, Embed embed, NoticeType noticeType)
        {
            using (var db = DataBase.DBContext.GetDbContext())
            {
                StreamVideo streamVideo = db.GetStreamVideoByVideoId(videolId);

                if (streamVideo == null)
                {
                    try
                    {
                        var item = await GetVideoAsync(videolId).ConfigureAwait(false);

                        var startTime = item.LiveStreamingDetails.ActualStartTime.Value;
                        streamVideo = new StreamVideo()
                        {
                            ChannelId = item.Snippet.ChannelId,
                            ChannelTitle = item.Snippet.ChannelTitle,
                            VideoId = item.Id,
                            VideoTitle = item.Snippet.Title,
                            ScheduledStartTime = startTime,
                            ChannelType = StreamVideo.YTChannelType.Other
                        };

                        if (!addNewStreamVideo.TryAdd(streamVideo, streamVideo.ChannelType))
                            return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message + "\r\n" + ex.StackTrace);
                        return;
                    }
                }

                await SendStreamMessageAsync(streamVideo, embed, noticeType).ConfigureAwait(false);
            }
        }
        

        private async Task SendStreamMessageAsync(StreamVideo streamVideo, Embed embed, NoticeType noticeType)
        {
            string type = streamVideo.ChannelType == StreamVideo.YTChannelType.Holo ? "holo" : streamVideo.ChannelType == StreamVideo.YTChannelType.Nijisanji ? "2434" : "other";
            List<NoticeYoutubeStreamChannel> noticeGuildList = new List<NoticeYoutubeStreamChannel>();

            using (var db = DataBase.DBContext.GetDbContext())
            {
                try
                {
                    noticeGuildList.AddRange(db.NoticeYoutubeStreamChannel.ToList().Where((x) => x.NoticeStreamChannelId == streamVideo.ChannelId));
                }
                catch (Exception ex)
                {
                    Log.Error($"SendStreamMessageAsyncChannel {streamVideo.VideoId} - {ex.Message}\r\n{ex.StackTrace}");
                }

                //類型檢查
                try
                {
                    if (type != "other" || //如果不是其他類的頻道
                        !db.YoutubeChannelSpider.Any((x) => x.ChannelId == streamVideo.ChannelId) || //或該頻道非在爬蟲清單內
                        !db.YoutubeChannelSpider.FirstOrDefault((x) => x.ChannelId == streamVideo.ChannelId).IsWarningChannel) //或該爬蟲非警告類的頻道
                    {
                        noticeGuildList.AddRange(db.NoticeYoutubeStreamChannel.ToList().Where((x) => x.NoticeStreamChannelId == "all" || x.NoticeStreamChannelId == type));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"SendStreamMessageAsyncOtherChannel {streamVideo.VideoId} - {ex.Message}\r\n{ex.StackTrace}");
                }

                Log.NewStream($"發送直播通知 ({noticeGuildList.Count} / {noticeType}): {streamVideo.ChannelTitle} - {streamVideo.VideoTitle}");

                foreach (var item in noticeGuildList)
                {
                    try
                    {
                        string sendMessage = "";
                        switch (noticeType)
                        {
                            case NoticeType.NewStream:
                                sendMessage = item.NewStreamMessage;
                                break;
                            case NoticeType.NewVideo:
                                sendMessage = item.NewVideoMessage;
                                break;
                            case NoticeType.Start:
                                sendMessage = item.StratMessage;
                                break;
                            case NoticeType.End:
                                sendMessage = item.EndMessage;
                                break;
                            case NoticeType.ChangeTime:
                                sendMessage = item.ChangeTimeMessage;
                                break;
                            case NoticeType.Delete:
                                sendMessage = item.DeleteMessage;
                                break;
                        }
                        if (sendMessage == "-") continue;

                        var guild = _client.GetGuild(item.GuildId);
                        if (guild == null) continue;
                        var channel = guild.GetTextChannel(item.DiscordChannelId);
                        if (channel == null) continue;

                        await channel.SendMessageAsync(sendMessage, false, embed);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Notice Youtube {item.GuildId} / {item.DiscordChannelId}\r\n{ex.Message}");
                        if (ex.Message.Contains("50013") || ex.Message.Contains("50001")) db.NoticeYoutubeStreamChannel.Remove(db.NoticeYoutubeStreamChannel.First((x) => x.DiscordChannelId == item.DiscordChannelId));
                        await db.SaveChangesAsync();
                    }
                }
            }
        }

        public async Task<Video> GetVideoAsync(string videoId)
        {
            try
            {
                var video = yt.Videos.List("snippet,liveStreamingDetails");
                video.Id = videoId;
                var videoResult = await video.ExecuteAsync().ConfigureAwait(false);
                if (videoResult.Items.Count == 0) return null;
                return videoResult.Items[0];
            }
            catch (Exception ex)
            {
                Log.Error($"GetVideoAsync {ex.Message}\r\n{ex.StackTrace}");
                return null;
            }
        }

        private async Task<IEnumerable<Video>> GetVideosAsync(IEnumerable<string> videoIds)
        {
            try
            {
                var video = yt.Videos.List("snippet,liveStreamingDetails");
                video.Id = string.Join(',', videoIds);
                var videoResult = await video.ExecuteAsync().ConfigureAwait(false);
                return videoResult.Items;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}