using Volo.Abp.Modularity;

namespace Ke.Chat;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class ChatDomainTestBase<TStartupModule> : ChatTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
