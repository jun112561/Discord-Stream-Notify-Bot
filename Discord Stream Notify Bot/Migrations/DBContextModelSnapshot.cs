﻿// <auto-generated />
using System;
using Discord_Stream_Notify_Bot.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Discord_Stream_Notify_Bot.Migrations
{
    [DbContext(typeof(DBContext))]
    partial class DBContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.1");

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.BannerChange", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LastChangeStreamId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("BannerChange");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.GuildConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("MemberCheckChannelId")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("MemberCheckGrantRoleId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("MemberCheckVideoId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("GuildConfig");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.HoloStreamVideo", b =>
                {
                    b.Property<string>("VideoId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelTitle")
                        .HasColumnType("TEXT");

                    b.Property<int>("ChannelType")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("ScheduledStartTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("VideoTitle")
                        .HasColumnType("TEXT");

                    b.HasKey("VideoId");

                    b.ToTable("HoloStreamVideo");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.MemberAccessToken", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("DiscordUserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("GoogleAccessToken")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("GoogleExpiresIn")
                        .HasColumnType("TEXT");

                    b.Property<string>("GoogleRefrechToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("YoutubeChannelId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("MemberAccessToken");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.NijisanjiStreamVideo", b =>
                {
                    b.Property<string>("VideoId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelTitle")
                        .HasColumnType("TEXT");

                    b.Property<int>("ChannelType")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("ScheduledStartTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("VideoTitle")
                        .HasColumnType("TEXT");

                    b.HasKey("VideoId");

                    b.ToTable("NijisanjiStreamVideo");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.NoticeTwitterSpaceChannel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("DiscordChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("NoticeTwitterSpaceUserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("NoticeTwitterSpaceUserScreenName")
                        .HasColumnType("TEXT");

                    b.Property<string>("StratTwitterSpaceMessage")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("NoticeTwitterSpaceChannel");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.NoticeYoutubeStreamChannel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ChangeTimeMessage")
                        .HasColumnType("TEXT");

                    b.Property<string>("DeleteMessage")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("DiscordChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("EndMessage")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("NewStreamMessage")
                        .HasColumnType("TEXT");

                    b.Property<string>("NewVideoMessage")
                        .HasColumnType("TEXT");

                    b.Property<string>("NoticeStreamChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("StratMessage")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("NoticeYoutubeStreamChannel");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.OtherStreamVideo", b =>
                {
                    b.Property<string>("VideoId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelTitle")
                        .HasColumnType("TEXT");

                    b.Property<int>("ChannelType")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("ScheduledStartTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("VideoTitle")
                        .HasColumnType("TEXT");

                    b.HasKey("VideoId");

                    b.ToTable("OtherStreamVideo");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.RecordYoutubeChannel", b =>
                {
                    b.Property<string>("YoutubeChannelId")
                        .HasColumnType("TEXT");

                    b.HasKey("YoutubeChannelId");

                    b.ToTable("RecordYoutubeChannel");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.TwitterSpace", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("SpaecActualStartTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("SpaecId")
                        .HasColumnType("TEXT");

                    b.Property<string>("SpaecMasterPlaylistUrl")
                        .HasColumnType("TEXT");

                    b.Property<string>("SpaecTitle")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserName")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserScreenName")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("TwitterSpace");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.TwitterSpaecSpider", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsRecord")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsWarningUser")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserName")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserScreenName")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId");

                    b.ToTable("TwitterSpaecSpider");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.YoutubeChannelOwnedType", b =>
                {
                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelTitle")
                        .HasColumnType("TEXT");

                    b.Property<int>("ChannelType")
                        .HasColumnType("INTEGER");

                    b.HasKey("ChannelId");

                    b.ToTable("YoutubeChannelOwnedType");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.YoutubeChannelSpider", b =>
                {
                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ChannelTitle")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsWarningChannel")
                        .HasColumnType("INTEGER");

                    b.HasKey("ChannelId");

                    b.ToTable("YoutubeChannelSpider");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.YoutubeMemberCheck", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("GuildConfigId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("LastCheckStatus")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("LastCheckTime")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildConfigId");

                    b.ToTable("YoutubeMemberCheck");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.YoutubeMemberCheck", b =>
                {
                    b.HasOne("Discord_Stream_Notify_Bot.DataBase.Table.GuildConfig", null)
                        .WithMany("MemberCheck")
                        .HasForeignKey("GuildConfigId");
                });

            modelBuilder.Entity("Discord_Stream_Notify_Bot.DataBase.Table.GuildConfig", b =>
                {
                    b.Navigation("MemberCheck");
                });
#pragma warning restore 612, 618
        }
    }
}
