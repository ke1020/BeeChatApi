using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Ke.Chat.Chats;
using Volo.Abp.EntityFrameworkCore.Modeling;

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
            b.Property(e => e.Content);
        });
    }
}
