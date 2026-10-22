using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ZodiacBuddy.Stages.Animus.Leves;

/// <summary>
/// Pure matching rules for Animus leve issuers and their SelectString menus.
/// NPC ids come from Leve.LevelLevemete; menu labels come from the client sheet or from
/// phrases verified in the Chinese client's GuildleveAssignment text.
/// </summary>
internal static class LeveIssuerSelection
{
    internal const byte EventNpcLevelType = 8;
    internal const byte BattlecraftAssignmentType = 1;
    internal const byte MaelstromAssignmentType = 13;
    internal const byte ImmortalFlamesAssignmentType = 15;
    internal const float IssuerPositionTolerance = 20f;

    /// <summary>Addon 594. SelectString label for LeveAssignmentType 1, whose sheet name is 佣兵作业.</summary>
    internal const string BattlecraftMenuLabel = "佣兵任务";

    /// <summary>Addon 595. SelectString groups miner, botanist, and fisher; it is not those job names.</summary>
    internal const string FieldcraftMenuLabel = "采集任务";

    /// <summary>Addon 596. SelectString groups the crafting jobs; it is not those job names.</summary>
    internal const string TradecraftMenuLabel = "制作任务";

    /// <summary>Addon 4570. Grand Company supply entry on the same menu as company leves.</summary>
    internal const string SupplyMenuLabel = "筹备任务";

    /// <summary>Substring of GuildleveAssignment.Type「军队理符发行处」. This is the issuer's function title, not a SelectString entry.</summary>
    internal const string GrandCompanyBridgeLabel = "军队理符";

    /// <summary>Substring of GuildleveAssignment.Type「行会理符发行处」. This is the issuer's function title, not a SelectString entry.</summary>
    internal const string GuildBridgeLabel = "行会理符";

    internal readonly record struct MenuEntry(int Index, string Text);

    internal readonly record struct Candidate(
        uint DataId,
        uint BaseId,
        uint TerritoryId,
        Vector3 Position,
        bool Targetable,
        bool IsEventNpc);

    internal static bool IsGrandCompany(byte assignmentType)
        => assignmentType is >= MaelstromAssignmentType and <= ImmortalFlamesAssignmentType;

    internal static bool MatchesIssuer(
        Candidate candidate,
        uint expectedDataId,
        uint expectedTerritoryId,
        Vector3 expectedPosition,
        out float distanceFromSheet)
    {
        distanceFromSheet = float.MaxValue;
        if (!candidate.IsEventNpc || !candidate.Targetable || expectedDataId == 0)
            return false;
        if (candidate.TerritoryId != expectedTerritoryId)
            return false;
        if (candidate.DataId != expectedDataId && candidate.BaseId != expectedDataId)
            return false;

        distanceFromSheet = Vector3.Distance(candidate.Position, expectedPosition);
        return distanceFromSheet <= IssuerPositionTolerance;
    }

    internal static int? ChooseAcquire(
        byte assignmentType,
        string assignmentLabel,
        IReadOnlyList<MenuEntry> entries,
        out string detail)
    {
        if (entries.Count == 0)
        {
            detail = "SelectString has no entries.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(assignmentLabel))
        {
            detail = $"Assignment type {assignmentType} has no client label.";
            return null;
        }

        var specific = FindContaining(entries, assignmentLabel);
        if (specific != null)
        {
            detail = $"matched client assignment label '{assignmentLabel}'.";
            return specific.Value.Index;
        }

        var menuLabel = VerifiedMenuLabel(assignmentType);
        if (menuLabel != null)
        {
            var menu = FindContaining(entries, menuLabel);
            if (menu != null)
            {
                detail = $"matched Addon menu label '{menuLabel}' for assignment '{assignmentLabel}'.";
                return menu.Value.Index;
            }
        }

        detail = menuLabel == null
            ? $"No menu entry contains the client label '{assignmentLabel}'."
            : $"No menu entry contains '{assignmentLabel}' or the Addon label '{menuLabel}'.";
        return null;
    }

    internal static int? ChooseTurnIn(
        IReadOnlyList<string> categoryLabels,
        IReadOnlyList<MenuEntry> entries,
        out string detail)
    {
        if (entries.Count == 0)
        {
            detail = "SelectString has no entries.";
            return null;
        }

        var report = entries.FirstOrDefault(entry =>
            entry.Text.Contains("报告", StringComparison.Ordinal)
            && !IsAcquisitionEntry(entry.Text, categoryLabels));
        if (!string.IsNullOrEmpty(report.Text))
        {
            detail = $"matched completion wording in '{report.Text}'.";
            return report.Index;
        }

        var first = entries[0];
        if (!IsAcquisitionEntry(first.Text, categoryLabels))
        {
            detail = $"selected index {first.Index} '{first.Text}' because it is not an acquisition category.";
            return first.Index;
        }

        detail = $"Refusing index {first.Index} '{first.Text}' because it is an acquisition category and no 报告 entry is present.";
        return null;
    }

    internal static int? ChooseExit(
        IReadOnlyList<string> categoryLabels,
        IReadOnlyList<MenuEntry> entries,
        out string detail)
    {
        if (entries.Count == 0)
        {
            detail = "SelectString has no entries.";
            return null;
        }

        var last = entries[^1];
        if (IsAcquisitionEntry(last.Text, categoryLabels))
        {
            detail = $"Refusing to close via index {last.Index} '{last.Text}' because it is an acquisition category.";
            return null;
        }

        detail = $"closing via last index {last.Index} '{last.Text}'.";
        return last.Index;
    }

    internal static bool IsAcquisitionEntry(string text, IReadOnlyList<string> categoryLabels)
    {
        if (text.Contains(BattlecraftMenuLabel, StringComparison.Ordinal)
            || text.Contains(FieldcraftMenuLabel, StringComparison.Ordinal)
            || text.Contains(TradecraftMenuLabel, StringComparison.Ordinal)
            || text.Contains(SupplyMenuLabel, StringComparison.Ordinal)
            || text.Contains(GrandCompanyBridgeLabel, StringComparison.Ordinal)
            || text.Contains(GuildBridgeLabel, StringComparison.Ordinal))
            return true;

        foreach (var label in categoryLabels)
        {
            if (!string.IsNullOrWhiteSpace(label) && text.Contains(label, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static string? VerifiedMenuLabel(byte assignmentType) => assignmentType switch
    {
        BattlecraftAssignmentType => BattlecraftMenuLabel,
        >= 2 and <= 4 => FieldcraftMenuLabel,
        >= 5 and <= 12 => TradecraftMenuLabel,
        _ => null,
    };

    private static MenuEntry? FindContaining(IReadOnlyList<MenuEntry> entries, string needle)
    {
        foreach (var entry in entries)
        {
            if (entry.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }
}
