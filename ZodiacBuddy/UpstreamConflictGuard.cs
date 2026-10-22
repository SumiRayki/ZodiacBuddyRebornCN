using System.Linq;

namespace ZodiacBuddy;

/// <summary>
/// Detects whether the upstream ZodiacBuddy Reborn plugin is loaded right now.
/// </summary>
internal static class UpstreamConflictGuard
{
    internal static bool IsLoaded()
    {
        var plugins = Service.Interface.InstalledPlugins;
        if (plugins == null)
            return false;

        return UpstreamPluginPresence.IsUpstreamActive(
            plugins.Select(plugin => new PluginLoadState(plugin.InternalName, plugin.IsLoaded)));
    }
}
