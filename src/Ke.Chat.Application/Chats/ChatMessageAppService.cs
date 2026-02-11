using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.Models.Chats;

namespace Ke.Chat.Chats;

public class ChatSessionAppService(IChatSessionRepository sessionRepository,
    IChatMessageRepository messageRepository) :
    ChatAppService,
    IChatSessionAppService
{
    private readonly IChatSessionRepository _sessionRepository = sessionRepository;
    private readonly IChatMessageRepository _messageRepository = messageRepository;

    public async Task CreateAsync(CreateChatSessionDto input,
        CancellationToken cancellationToken = default)
    {
        await _sessionRepository.InsertAsync(new ChatSession(input.SessionId)
        {
            Title = input.Title,
            Messages = [.. input.Messages.Select(msg => ParseMessage(input.SessionId, msg))]
        }, false, cancellationToken);
    }

    public async Task AddMessageAsync(Guid sessionId,
        ChatMessageInputDto input,
        CancellationToken cancellationToken = default)
    {
        await _messageRepository.InsertAsync(ParseMessage(sessionId, input), false, cancellationToken);
    }

    private ChatMessage ParseMessage(Guid sessionId, ChatMessageInputDto input)
    {
        return new ChatMessage(GuidGenerator.Create(),
                input.MessageId,
                input.Model,
                ChatRole.User)
        {
            SessionId = sessionId,
            ParentId = input.ParentId,
            ThinkingEnabled = input.ThinkingEnabled,
            BanEdit = input.BanEdit,
            BanRegenerate = input.BanRegenerate,
            Status = ChatStatus.Pending, //chatMessage.Status,
            AccumulatedTokenUsage = input.AccumulatedTokenUsage,
            Files = input.Files,
            SearchEnabled = input.SearchEnabled,
            Fragments = input.Fragments?.Select(ParseFragment)?.ToList() ?? []
        };
    }

    private MessageFragment ParseFragment(ChatMessageFragment fm)
    {
        var type = Enum.TryParse<FragmentType>(fm.Type, out var ft) ? ft : FragmentType.None;
        // 根据决定内容类型
        FragmentContent content = type switch
        {
            FragmentType.THINK => new TaskFragmentContent(new TaskInfo()),
            _ => new TextFragmentContent(string.Empty)
        };

        var fragment = new MessageFragment(GuidGenerator.Create(), type);
        fragment.SetContent(content);
        return fragment;
    }

    public class MessageFragmentContent
    {
        public string? Content { get; set; }
        public List<TaskItem>? Tasks { get; set; }
    }
}

/*
public class ChatMessageAppService : ChatAppService, IMessageAppService
{
    private readonly IChatSessionRepository _sessionRepository;

    public ChatMessageAppService(IChatSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }


    public async Task CreateAsync(Guid sessionId, Tasks.Models.Chats.ChatMessage chatMessage, CancellationToken cancellationToken = default)
    {
        await _sessionRepository.InsertAsync(new ChatMessage(
            Guid.CreateVersion7(),
            chatMessage.MessageId,
            chatMessage.Model,
            ChatRole.User)
        {
            ParentId = chatMessage.ParentId,
            ThinkingEnabled = chatMessage.ThinkingEnabled,
            BanEdit = chatMessage.BanEdit,
            BanRegenerate = chatMessage.BanRegenerate,
            Status = ChatStatus.Pending, //chatMessage.Status,
            AccumulatedTokenUsage = chatMessage.AccumulatedTokenUsage,
            Files = chatMessage.Files,
            SearchEnabled = chatMessage.SearchEnabled,
            Fragments = (chatMessage.Fragments?.Select(x => new MessageFragment(
                    Guid.CreateVersion7(),
                    Enum.TryParse<FragmentType>(x.Type, out var ft) ? ft : FragmentType.None)
            {
                Content = x.Content,
                Tasks = x.Tasks != null ? JsonSerializer.Serialize(x.Tasks) : null
            }))?.ToList() ?? []
        }, cancellationToken: cancellationToken);
    }
}
*/