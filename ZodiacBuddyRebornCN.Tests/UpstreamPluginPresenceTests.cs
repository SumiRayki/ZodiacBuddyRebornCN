using Xunit;
using ZodiacBuddy;

namespace ZodiacBuddyRebornCN.Tests;

public class UpstreamPluginPresenceTests
{
    [Fact]
    public void Active_upstream_plugin_is_a_conflict()
    {
        var plugins = new[]
        {
            new PluginLoadState("ZodiacBuddyRebornCN", true),
            new PluginLoadState("ZodiacBuddyReborn", true),
        };

        Assert.True(UpstreamPluginPresence.IsUpstreamActive(plugins));
    }

    [Fact]
    public void Disabled_upstream_plugin_is_not_a_conflict()
    {
        var plugins = new[]
        {
            new PluginLoadState("ZodiacBuddyRebornCN", true),
            new PluginLoadState("ZodiacBuddyReborn", false),
        };

        Assert.False(UpstreamPluginPresence.IsUpstreamActive(plugins));
    }
}
