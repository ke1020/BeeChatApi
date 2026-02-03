using Ke.Tasks.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Ke.Tasks;

public static class TasksConfigureExtensions
{
    public static IServiceCollection AddTasks(this IServiceCollection services)
    {
        services.AddSingleton<IEventBufferService, EventBufferService>();
        //services.AddSingleton<ISpeechRecognitionNotification, SpeechRecognitionNotification>();
        //services.AddHostedService<EventCleanupService>();
        services.AddSingleton<IChat, ChatCompletion>();
        return services;
    }
}