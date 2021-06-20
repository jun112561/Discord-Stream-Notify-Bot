﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord_Stream_Notify_Bot.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Discord_Stream_Notify_Bot.Command.Admin
{
    public class Administration : TopLevelModule<AdministraitonService>
    {
        private readonly DiscordSocketClient _client;
        public Administration(DiscordSocketClient discordSocketClient)
        {
            _client = discordSocketClient;
        }

        [RequireContext(ContextType.Guild)]
        [RequireOwner]
        [Command("Clear")]
        [Summary("清除機器人的發言")]
        public async Task Clear()
        {
            await _service.ClearUser((ITextChannel)Context.Channel);
        }

        [Command("UpdateStatus")]
        [Summary("更新機器人的狀態\n參數: Guild, Member, Stream, Info")]
        [Alias("UpStats")]
        [RequireOwner]
        public async Task UpdateStatusAsync([Summary("狀態")] string stats)
        {
            switch (stats.ToLowerInvariant())
            {
                case "guild":
                    Program.updateStatus = Program.UpdateStatus.Guild;
                    break;
                case "member":
                    Program.updateStatus = Program.UpdateStatus.Member;
                    break;
                case "stream":
                    Program.updateStatus = Program.UpdateStatus.Stream;
                    break;
                case "info":
                    Program.updateStatus = Program.UpdateStatus.Info;
                    break;
                default:
                    await Context.Channel.SendConfirmAsync(string.Format("找不到 {0} 狀態", stats));
                    return;
            }
            Program.ChangeStatus();
            return;
        }

        [Command("Say")]
        [Summary("說話")]
        [RequireOwner]
        public async Task SayAsync([Summary("內容")][Remainder] string text)
        {
            await Context.Channel.SendConfirmAsync(text);
        }

        [Command("ListServer")]
        [Summary("顯示所有的伺服器")]
        [Alias("LS")]
        [RequireOwner]
        public async Task ListServerAsync([Summary("頁數")] int page = 0)
        {
            await Context.SendPaginatedConfirmAsync(page, (cur) =>
            {
                EmbedBuilder embedBuilder = new EmbedBuilder().WithOkColor().WithTitle("目前所在的伺服器有");

                foreach (var item in _client.Guilds.Skip(cur * 5).Take(5))
                {
                    int totalMember = item.MemberCount;
                    bool isBotOwnerInGuild = item.GetUser(Program.ApplicatonOwner.Id) != null;

                    embedBuilder.AddField(item.Name, "Id: " + item.Id +
                        "\nOwner Id: " + item.OwnerId +
                        "\n人數: " + totalMember.ToString() +
                        "\nBot擁有者是否在該伺服器: " + (isBotOwnerInGuild ? "是" : "否"));
                }

                return embedBuilder;
            }, _client.Guilds.Count, 5);
        }

        [Command("Die")]
        [Summary("關閉機器人")]
        [Alias("Bye")]
        [RequireOwner]
        public async Task DieAsync()
        {
            Program.isDisconnect = true;
            await Context.Channel.SendConfirmAsync("關閉中");
        }

        [Command("Leave")]
        [Summary("讓機器人離開指定的伺服器")]
        [RequireOwner]
        public async Task LeaveAsync([Summary("伺服器Id")] ulong gid = 0)
        {
            if (gid == 0) { await Context.Channel.SendConfirmAsync("伺服器Id為空"); return; }

            try { await _client.GetGuild(gid).LeaveAsync(); }
            catch (Exception) { await Context.Channel.SendConfirmAsync("失敗，請確認Id是否正確"); return; }

            await Context.Channel.SendConfirmAsync("✅");
        }

        [Command("GetInviteURL")]
        [Summary("取得伺服器的邀請連結")]
        [RequireBotPermission(GuildPermission.CreateInstantInvite)]
        [RequireOwner]
        public async Task GetInviteURLAsync([Summary("伺服器Id")] ulong gid = 0, [Summary("頻道Id")] ulong cid = 0)
        {
            if (gid == 0) gid = Context.Guild.Id;
            SocketGuild guild = _client.GetGuild(gid);

            try
            {
                if (cid == 0)
                {
                    IReadOnlyCollection<SocketTextChannel> socketTextChannels = guild.TextChannels;

                    await Context.SendPaginatedConfirmAsync(0, (cur) =>
                    {
                        EmbedBuilder embedBuilder = new EmbedBuilder()
                           .WithOkColor()
                           .WithTitle("以下為 " + guild.Name + " 所有的文字頻道")
                           .WithDescription(string.Join('\n', socketTextChannels.Skip(cur * 10).Take(10).Select((x) => x.Id + " / " + x.Name)));

                        return embedBuilder;
                    }, socketTextChannels.Count, 10);
                }
                else
                {
                    IInviteMetadata invite = await guild.GetTextChannel(cid).CreateInviteAsync(300, 1, false);
                    await Context.Channel.SendConfirmAsync(invite.Url);
                }
            }
            catch (Exception ex) { Log.FormatColorWrite(ex.Message + "\r\n" + ex.StackTrace, ConsoleColor.Red); }
        }

        [Command("SendMsgToAllGuild")]
        [Summary("傳送訊息到所有伺服器")]
        [Alias("GuildMsg")]
        [RequireOwner]
        public async Task SendMsgToAllGuild(string imageUrl = "", [Remainder] string message = "")
        {
            if (message == "")
            {
                await Context.Channel.SendConfirmAsync("訊息為空");
                return;
            }

            if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription(message).WithImageUrl(imageUrl)))
            {
                using (var uow = new DBContext())
                {
                    EmbedBuilder embedBuilder = new EmbedBuilder().WithOkColor()
                        .WithUrl("https://twitter.com/jun112561")
                        .WithTitle("來自開發者消息")
                        .WithAuthor(Context.Message.Author)
                        .WithDescription(message)
                        .WithImageUrl(imageUrl)
                        .WithFooter("若看到此消息出現在非通知頻道上，請通知管理員重新設定直播通知");

                    try
                    {
                        int i = 1, num = _client.Guilds.Count;
                        var list = uow.NoticeStreamChannel.Distinct((x) => x.GuildId).Select((x) => new KeyValuePair<ulong, ulong>(x.GuildId, x.ChannelId));
                        foreach (var item in _client.Guilds)
                        {
                            try
                            {
                                SocketTextChannel channel;
                                if (list.Any((x) => x.Key == item.Id))
                                {
                                    var cid = list.First((x) => x.Key == item.Id).Value;
                                    channel = item.GetTextChannel(cid);
                                }
                                else
                                {
                                    channel = item.TextChannels.First((x) => item.GetUser(_client.CurrentUser.Id).GetPermissions(x).SendMessages);
                                }

                                await channel.SendMessageAsync(embed: embedBuilder.Build());
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"MSG: {item.Name}({item.Id})");
                                Log.Error(ex.Message);

                                try
                                {
                                    uow.NoticeStreamChannel.RemoveRange(Queryable.Where(uow.NoticeStreamChannel, (x) => x.GuildId == item.Id));
                                    await uow.SaveChangesAsync();
                                }
                                catch { }
                            }
                            finally
                            {
                                Log.Info($"({i++}/{num}) {item.Name}");
                            }
                        }                 
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"{ex.Message}\r\n{ex.StackTrace}");
                    }

                    await Context.Channel.SendConfirmAsync("已發送完成");
                }
            }
        }
    }
}