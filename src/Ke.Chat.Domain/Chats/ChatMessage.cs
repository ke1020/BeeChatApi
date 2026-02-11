using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace Ke.Chat.Chats;

public class ChatMessage : AuditedAggregateRoot<Guid>
{
    public int MessageId { get; set; }
    public int? ParentId { get; set; }
    public string? Model { get; set; }
    public ChatRole? Role { get; set; }
    public bool ThinkingEnabled { get; set; } = false;
    public bool BanEdit { get; set; } = false;
    public bool BanRegenerate { get; set; } = false;
    public ChatStatus Status { get; set; } = ChatStatus.Pending;
    public int AccumulatedTokenUsage { get; set; } = 0;
    public string[]? Files { get; set; } = [];
    public bool SearchEnabled { get; set; } = false;
    public List<MessageFragment> Fragments { get; set; } = [];

    public Guid SessionId { get; set; }
    public ChatSession? Session { get; set; }

    private ChatMessage()
    {
    }

    public ChatMessage(
        Guid id,
        int messageId,
        string? model = null,
        ChatRole? role = null)
        : base(id)
    {
        MessageId = messageId;
        Model = model;
        Role = role ?? ChatRole.None;
    }
}
