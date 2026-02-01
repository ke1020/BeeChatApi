using Ke.Tasks.SSE.Models;

namespace Ke.Tasks.Abstractions;

/// <summary>
/// 事件缓冲服务接口
/// </summary>
public interface IEventBufferService : IDisposable
{
    void AddEvent(SseEvent sseEvent);
    IEnumerable<SseEvent> GetEventsSince(string lastEventId);
    void ClearOldEvents(int? maxAgeInMinutes = null);
    void AddClient(string clientId, string lastEventId);
    bool UpdateClientLastEventId(string clientId, string lastEventId);
    IEnumerable<SseClient> GetConnectedClients();
}