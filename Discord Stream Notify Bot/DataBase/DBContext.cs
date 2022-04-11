﻿using Discord_Stream_Notify_Bot.DataBase.Table;
using Microsoft.EntityFrameworkCore;

namespace Discord_Stream_Notify_Bot.DataBase
{
    public class DBContext : DbContext
    {
        public DbSet<GuildConfig> GuildConfig { get; set; }
        public DbSet<BannerChange> BannerChange { get; set; }
        public DbSet<HoloStreamVideo> HoloStreamVideo { get; set; }
        public DbSet<MemberAccessToken> MemberAccessToken { get; set; }
        public DbSet<NijisanjiStreamVideo> NijisanjiStreamVideo { get; set; }
        public DbSet<NoticeTwitterSpaceChannel> NoticeTwitterSpaceChannel { get; set; }
        public DbSet<NoticeYoutubeStreamChannel> NoticeYoutubeStreamChannel { get; set; }
        public DbSet<OtherStreamVideo> OtherStreamVideo { get; set; }
        public DbSet<RecordYoutubeChannel> RecordYoutubeChannel { get; set; }
        public DbSet<TwitterSpace> TwitterSpace { get; set; }
        public DbSet<TwitterSpaecSpider> TwitterSpaecSpider { get; set; }
        public DbSet<YoutubeChannelOwnedType> YoutubeChannelOwnedType { get; set; }
        public DbSet<YoutubeChannelSpider> YoutubeChannelSpider { get; set; }
        public DbSet<YoutubeMemberCheck> YoutubeMemberCheck { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={Program.GetDataFilePath("DataBase.db")}")
#if DEBUG
            .LogTo(System.Console.WriteLine)
#endif
            .EnableSensitiveDataLogging();

        public static DBContext GetDbContext()
        {
            var context = new DBContext();
            context.Database.SetCommandTimeout(60);
            var conn = context.Database.GetDbConnection();
            conn.Open();
            using (var com = conn.CreateCommand())
            {
                com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF";
                com.ExecuteNonQuery();
            }
            return context;
        }
    }
}
