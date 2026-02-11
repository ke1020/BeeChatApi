using System;
using Volo.Abp.Domain.Entities;

namespace Ke.Chat.Chats;

public class MessageFragment : Entity<Guid>
{
    public FragmentType Type { get; set; }
    public FragmentContent? Content { get; private set; }  // 使用值对象
    public Guid ChatMessageId { get; set; }
    public ChatMessage? Message { get; set; }

    private MessageFragment() { } // EF Core

    public MessageFragment(Guid id, FragmentType fragmentType)
        : base(id)
    {
        Type = fragmentType;
    }

    public void SetContent(FragmentContent content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }
}