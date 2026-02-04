using Ke.Tasks.Abstractions;
using Ke.Tasks.Processors;
using Microsoft.Extensions.DependencyInjection;

namespace Ke.Tasks;

public static class TasksConfigureExtensions
{
    public static IServiceCollection AddTasks(this IServiceCollection services)
    {
        services.AddSingleton<IEventBufferService, EventBufferService>();
        services.AddSingleton<ITaskProcessFactory, TaskProcessFactory>();
        //services.AddSingleton<ISpeechRecognitionNotification, SpeechRecognitionNotification>();
        //services.AddHostedService<EventCleanupService>();
        services.AddSingleton<IChat, ChatCompletion>();

        services.AddSingleton<AsrTaskProcessor>();
        services.AddSingleton<TtsTaskProcessor>();
        return services;
    }
}