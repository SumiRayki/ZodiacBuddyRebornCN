using System.Collections.Generic;

namespace ZodiacBuddy;

/// <summary>
/// One installed plugin's load state, without a Dalamud dependency.
/// </summary>
internal readonly record struct PluginLoadState(string? InternalName, bool IsLoaded);

/// <summary>
/// Decides whether the upstream ZodiacBuddy Reborn plugin is currently loaded.
/// A previously loaded assembly is not enough: Dalamud keeps it after the plugin is disabled.
/// </summary>
internal static class UpstreamPluginPresence
{
    internal const string UpstreamInternalName = "ZodiacBuddyReborn";

    internal static bool IsUpstreamActive(IEnumerable<PluginLoadState> plugins)
    {
        foreach (var plugin in plugins)
        {
            if (plugin.IsLoaded
                && string.Equals(plugin.InternalName, UpstreamInternalName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
