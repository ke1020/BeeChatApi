using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Ke.Chat.Chats;
using Volo.Abp.EntityFrameworkCore.Modeling;
using System.Text.Json;
using System;

namespace Ke.Chat.EntityFrameworkCore;

public static class ChatDbContextModelCreatingExtensions
{
    public static void ConfigureChat(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        // 配置 Session 表
        builder.Entity<ChatSession>(b =>
        {
            b.ToTable(ChatDbProperties.DbTablePrefix + "Sessions", ChatDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).HasMaxLength(256);
            b.Property(x => x.TitleType).IsRequired();
            b.Property(x => x.Agent).IsRequired();
            b.Property(x => x.Pinned).IsRequired();
            b.Property(x => x.LastMessageId).IsRequired();

            b.HasMany(c => c.Messages) // session 有多个 message
                .WithOne(c => c.Session)
                .HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                ;
        });

        // 配置对话信息表
        builder.Entity<ChatMessage>(b =>
        {
            b.ToTable(ChatDbProperties.DbTablePrefix + "Messages", ChatDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(cm => cm.MessageId).IsRequired();
            b.Property(cm => cm.ParentId);
            b.Property(cm => cm.Model).HasMaxLength(128);
            b.Property(cm => cm.Role).HasMaxLength(32).IsRequired();
            b.Property(cm => cm.ThinkingEnabled).IsRequired();
            b.Property(cm => cm.BanEdit).IsRequired();
            b.Property(cm => cm.BanRegenerate).IsRequired();
            b.Property(cm => cm.Status).HasMaxLength(32).IsRequired();
            b.Property(cm => cm.AccumulatedTokenUsage).IsRequired();
            b.Property(cm => cm.Files).HasMaxLength(2000);
            b.Property(cm => cm.SearchEnabled).IsRequired();

            b.HasMany(b => b.Fragments) // 消息有多个片段
                .WithOne(p => p.Message) // 片段属于消息
                .HasForeignKey(p => p.ChatMessageId) // 片段外键
                .OnDelete(DeleteBehavior.Cascade) // 删除消息时，删除片段
                ;
        });

        // 配置信息片段表
        builder.Entity<MessageFragment>(b =>
        {
            b.ToTable(ChatDbProperties.DbTablePrefix + "MessageFragments", ChatDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(e => e.Type).IsRequired();
            b.Property(e => e.Content)
                .HasConversion(content => SerializeContent(content),
                    json => DeserializeContent(json))
                .HasColumnType("nvarchar(max)")
                ;
        });
    }

    private static string? SerializeContent(FragmentContent? content)
    {
        if(content == null)
        {
            return null;
        }

        var wrapper = new ContentWrapper
        {
            Type = content.Type,
            Data = content switch
            {
                TextFragmentContent text => JsonSerializer.Serialize(new { text.Text }),
                TaskFragmentContent task => JsonSerializer.Serialize(new { task.Task }),
                _ => throw new NotSupportedException($"Content type {content.GetType().Name} is not supported")
            }
        };

        return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static FragmentContent? DeserializeContent(string? json)
    {
        if(json == null)
        {
            return null;
        }
        
        var wrapper = JsonSerializer.Deserialize<ContentWrapper>(json);
        if (wrapper == null) return null;

        return wrapper.Type switch
        {
            FragmentContentType.Text =>
                JsonSerializer.Deserialize<TextFragmentContent>(wrapper.Data),
            FragmentContentType.Task =>
                JsonSerializer.Deserialize<TaskFragmentContent>(wrapper.Data),
            _ => throw new NotSupportedException($"Content type {wrapper.Type} is not supported")
        };
    }

    private class ContentWrapper
    {
        public FragmentContentType Type { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
