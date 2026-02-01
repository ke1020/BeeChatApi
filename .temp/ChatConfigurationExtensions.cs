using Ke.Chat.Controllers;
using Ke.Chat.SSE.Abstractions;
using Ke.Chat.SSE.Impl;
using Ke.Chat.Tasks.Impl;
using Ke.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ke.Chat;

public static class ChatConfigurationExtensions
{
    /// <summary>
    /// 配置文件最大上传大小
    /// </summary>
    private const long MaxFileSize = 2147483648;

    public static IServiceCollection AddChat(this IServiceCollection services)
    {
        services.Configure<FormOptions>(opts =>
        {
            opts.MultipartBodyLengthLimit = MaxFileSize;
        });

        services.Configure<KestrelServerOptions>(opts =>
        {
            opts.Limits.MaxRequestBodySize = MaxFileSize;
        });

        services.AddStorage(opts =>
        {
            new ConfigurationBuilder()
                .AddJsonFile(@"C:\Users\ke\dev\proj\abp\Basic\src\Storage\Ke.Storage.Test\Configs\storage.json", false, false)
                .Build()
                .Bind(opts)
                ;
        });

        return services;
    }

    public static IServiceCollection AddSse(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        services.AddSingleton<IEventBufferService, EventBufferService>();
        services.AddSingleton<ITaskProgress, TaskProgress>();
        services.AddHostedService<EventCleanupService>();

        return services;
    }
}