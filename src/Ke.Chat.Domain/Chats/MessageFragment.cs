using System;
using Volo.Abp.Domain.Entities;

namespace Ke.Chat.Chats;

public class MessageFragment : Entity<Guid>
{
    public FragmentType Type { get; set; }
    public string? Content { get; set; }
    public Guid ChatMessageId { get; set; }
    public ChatMessage? Message { get; set; }

    public MessageFragment()
    {

    }

    public MessageFragment(Guid id,
        FragmentType type)
    {
        Id = id;
        Type = type;
    }
}