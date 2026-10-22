using System.Linq;
using System.Reflection;

namespace ZodiacBuddy;

/// <summary>
/// Detects the upstream ZodiacBuddyReborn assembly. The CN fork keeps the same chat
/// commands and does not provide a safe side-by-side mode.
/// </summary>
internal static class UpstreamConflictGuard
{
    internal const string UpstreamAssemblyName = "ZodiacBuddyReborn";

    internal static bool IsLoaded()
        => AppDomainAssemblies().Any(assembly =>
            string.Equals(assembly.GetName().Name, UpstreamAssemblyName, System.StringComparison.OrdinalIgnoreCase));

    private static Assembly[] AppDomainAssemblies()
        => System.AppDomain.CurrentDomain.GetAssemblies();
}
