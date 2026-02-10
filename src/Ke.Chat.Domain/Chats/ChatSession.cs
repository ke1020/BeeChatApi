using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace Ke.Chat.Chats;

/// <summary>
/// 会话实体
/// </summary>
public class ChatSession : AuditedAggregateRoot<Guid>
{
    public required string Title { get; set; }
    public byte TitleType { get; set; } = (byte)Chats.TitleType.SYSTEM;
    public bool Pinned { get; set; } = false;
    public byte Agent { get; set; } = 0;
    public int LastMessageId { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];

    public ChatSession() { }

    public ChatSession(Guid id)
    {
        Id = id;
    }
}