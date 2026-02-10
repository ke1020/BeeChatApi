using System;

namespace Ke.Chat.Chats;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public int MessageId { get; set; }
    public int? ParentId { get; set; }
    public string? Model { get; set; }
    public ChatRole? Role { get; set; }
    public bool ThinkingEnabled { get; set; }
    public bool BanEdit { get; set; }
    public bool BanRegenerate { get; set; }
    public ChatStatus Status { get; set; }
    public int AccumulatedTokenUsage { get; set; }
    public string[]? Files { get; set; }
    public bool SearchEnabled { get; set; }
}

public class CreateChatMessageDto
{
    public int MessageId { get; set; }
    public int? ParentId { get; set; }
    public string? Model { get; set; }
    public ChatRole? Role { get; set; }
    public bool ThinkingEnabled { get; set; } = false;
    public bool BanEdit { get; set; } = false;
    public bool BanRegenerate { get; set; } = false;
    public ChatStatus Status { get; set; } = ChatStatus.Pending;
    public string[]? Files { get; set; }
    public bool SearchEnabled { get; set; } = false;
}

public class UpdateChatMessageDto
{
    public string? Model { get; set; }
    public ChatRole? Role { get; set; }
    public bool ThinkingEnabled { get; set; }
    public bool BanEdit { get; set; }
    public bool BanRegenerate { get; set; }
    public ChatStatus Status { get; set; }
    public int AccumulatedTokenUsage { get; set; }
    public string[]? Files { get; set; }
    public bool SearchEnabled { get; set; }
}

public class GetChatMessagesInput
{
    public int? MessageId { get; set; }
    public int? ParentId { get; set; }
    public ChatStatus? Status { get; set; }
    public int MaxResultCount { get; set; } = 10;
    public int SkipCount { get; set; } = 0;
}
