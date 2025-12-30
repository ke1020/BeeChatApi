using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;

namespace Ke.Chat.EntityFrameworkCore;

public class ChatHttpApiHostMigrationsDbContext : AbpDbContext<ChatHttpApiHostMigrationsDbContext>
{
    public ChatHttpApiHostMigrationsDbContext(DbContextOptions<ChatHttpApiHostMigrationsDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureChat();
        modelBuilder.ConfigureSettingManagement();
    }
}
