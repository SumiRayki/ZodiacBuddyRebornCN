using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lumina.Excel.Sheets;
using ZodiacBuddy.Stages.Animus.Data;
using ZodiacBuddy.Stages.Animus.Leves;
using Callback = ECommons.Automation.Callback;
using ZodiacBuddy.Systems.Leves;

namespace ZodiacBuddy.Stages.Animus;

internal partial class AnimusManager
{
    private const int IssuerResolveTimeoutSeconds = 20;
    private string[]? _assignmentCategoryLabels;
    private unsafe void DriveLeveAcquisition()
    {
        if (IsLeveAccepted(_leveContext.AutomationTarget.LeveId, out _))
        {
            if (_leveContext.AcquireCompletedAt == DateTime.MinValue)
            {
                _leveContext.AcquireCompletedAt = DateTime.Now;
                Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] Exact LeveId={_leveContext.AutomationTarget.LeveId} is accepted; unwinding the issuer menu before departure.");
            }

            if (TryExitLeveIssuerUi("after acceptance"))
                return;
            if ((DateTime.Now - _leveContext.AcquireCompletedAt).TotalMilliseconds < LeveIssuerExitSettleMs)
                return;

            _leveContext.AutomationState = LeveAutomationState.TravelingToStart;
            _leveContext.AutomationStatus = "Exact leve accepted and issuer menu closed; locating its start marker.";
            _leveContext.NoObjectiveSince = DateTime.Now.AddMilliseconds(500);
            Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] Issuer UI closed after accepting LeveId={_leveContext.AutomationTarget.LeveId}; moving to its start marker.");
            return;
        }

        if (TryDriveLeveTalk())
            return;

        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var yesNo) && yesNo != null && GenericHelpers.IsAddonReady(yesNo))
        {
            if (EzThrottler.Throttle("ZBR_LeveAcquireYesNo", 700))
            {
                var master = new AddonMaster.SelectYesno(yesNo);
                Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] Acquisition SelectYesno prompt='{master.Text}'. Choosing Yes.");
                master.Yes();
            }
            return;
        }

        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("GuildLeve", out var guildLeve) && guildLeve != null && GenericHelpers.IsAddonReady(guildLeve))
        {
            DriveGuildLeveSelection(guildLeve);
            return;
        }

        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectString", out var selectString) && selectString != null && GenericHelpers.IsAddonReady(selectString))
        {
            DriveLeveIssuerMenu(new AddonMaster.SelectString(selectString));
            return;
        }

        if (!TryResolveLeveIssuer(out var issuer))
            return;

        var player = Player.Object;
        if (player == null)
            return;

        var distance = Vector3.Distance(player.Position, issuer.Position);
        if (distance > LeveInteractDistance)
        {
            _leveContext.AutomationStatus = $"Issuer is {distance:F1}y away; approaching.";
            EnsureLeveFieldNavigation(issuer.Position, NavigationPurpose.LeveMapFlagTravel, "issuer approach");
            return;
        }

        if (Svc.Condition[ConditionFlag.Mounted])
        {
            EnqueueDismount();
            return;
        }

        if (EzThrottler.Throttle("ZBR_LeveIssuerInteract", 1200))
        {
            TargetSystem.Instance()->Target = (GameObject*)issuer.Address;
            TargetSystem.Instance()->InteractWithObject((GameObject*)issuer.Address, false);
            Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] Interacted with issuer '{issuer.Name.TextValue}' for LeveId={_leveContext.AutomationTarget.LeveId}.");
        }
    }
    private unsafe void DriveGuildLeveSelection(AtkUnitBase* guildLeve)
    {
        var selectedName = guildLeve->AtkValues[1233].String.Value == null
            ? string.Empty
            : guildLeve->AtkValues[1233].String.ToString();

        if (selectedName.Equals(_leveContext.AutomationTarget.Name, StringComparison.OrdinalIgnoreCase)
            && GenericHelpers.TryGetAddonByName<AtkUnitBase>("JournalDetail", out var journalDetail)
            && journalDetail != null
            && GenericHelpers.IsAddonReady(journalDetail))
        {
            var journal = new AddonMaster.JournalDetail(journalDetail);
            if (EzThrottler.Throttle("ZBR_LeveAcceptExact", 800))
            {
                Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] Accepting exact LeveId={_leveContext.AutomationTarget.LeveId} from JournalDetail.");
                journal.AcceptMap();
            }
            return;
        }

        var count = guildLeve->AtkValues[25].UInt;
        for (var i = 0; i < count; i++)
        {
            var value = guildLeve->AtkValues[626 + i * 2];
            if (!IsLeveGuildStringValue(value.Type))
                break;

            var name = value.String.ToString();
            if (!name.Equals(_leveContext.AutomationTarget.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (EzThrottler.Throttle("ZBR_LeveSelectExact", 700))
            {
                Callback.Fire(guildLeve, true, 13, i, (int)_leveContext.AutomationTarget.LeveId);
                Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] Selected exact GuildLeve offering index={i}, LeveId={_leveContext.AutomationTarget.LeveId}, name='{name}'.");
            }
            return;
        }

        var unavailableMessage = $"Exact LeveId={_leveContext.AutomationTarget.LeveId} '{_leveContext.AutomationTarget.Name}' is not currently offered by {GetLeveIssuerName(_leveContext.AutomationTarget.Issuer)}.";
        if (_leveContext.AllowUnavailableReturn)
            BeginLeveUnavailableClose(unavailableMessage);
        else
            FailLeveAutomation(unavailableMessage);
    }
    private static bool IsLeveGuildStringValue(AtkValueType type)
        => type is AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString;
    private static unsafe bool TryDriveLeveTalk()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("Talk", out var talk) || talk == null || !GenericHelpers.IsAddonReady(talk))
            return false;

        if (EzThrottler.Throttle("ZBR_LeveTalk", 350))
            new AddonMaster.Talk(talk).Click();
        return true;
    }
    private unsafe IGameObject? FindLeveIssuer(LeveObjectiveDefinition target)
    {
        var territory = Svc.ClientState.TerritoryType;
        IGameObject? best = null;
        var bestDistance = float.MaxValue;
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == nint.Zero)
                continue;

            var identity = ReadObjectIdentity(obj);
            var candidate = new LeveIssuerSelection.Candidate(
                identity.DataId,
                identity.BaseId,
                territory,
                obj.Position,
                obj.IsTargetable,
                obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc);
            if (!LeveIssuerSelection.MatchesIssuer(candidate, target.IssuerDataId, target.IssuerTerritoryId, target.IssuerPosition, out var distance) || distance >= bestDistance)
                continue;

            best = obj;
            bestDistance = distance;
        }

        return best;
    }

    private unsafe bool TryResolveLeveIssuer(out IGameObject issuer)
    {
        var found = FindLeveIssuer(_leveContext.AutomationTarget);
        if (found != null)
        {
            _leveContext.IssuerUnresolvedSince = DateTime.MinValue;
            issuer = found;
            return true;
        }

        issuer = null!;
        if (_leveContext.IssuerUnresolvedSince == DateTime.MinValue)
            _leveContext.IssuerUnresolvedSince = DateTime.Now;

        var target = _leveContext.AutomationTarget;
        var waited = DateTime.Now - _leveContext.IssuerUnresolvedSince;
        var detail = DescribeNearbyIssuers(target);
        _leveContext.AutomationStatus = $"Waiting for issuer {target.IssuerDataId} ({target.Issuer}).";
        if (EzThrottler.Throttle("ZBR_LeveIssuerMiss", 5000))
        {
            Service.PluginLog.Information(
                $"[ZodiacBuddy/LEVE] LeveId={target.LeveId} state={_leveContext.AutomationState} expectedIssuer={target.IssuerDataId} '{target.Issuer}' expectedTerritory={target.IssuerTerritoryId} currentTerritory={Svc.ClientState.TerritoryType} waited={waited.TotalSeconds:F0}s candidates: {detail}");
        }

        if (waited.TotalSeconds >= IssuerResolveTimeoutSeconds)
        {
            FailLeveAutomation(
                $"Issuer data id {target.IssuerDataId} ({target.Issuer}) was not found for LeveId={target.LeveId} after {IssuerResolveTimeoutSeconds}s. Candidates: {detail}");
        }

        return false;
    }

    private unsafe string DescribeNearbyIssuers(LeveObjectiveDefinition target)
    {
        var territory = Svc.ClientState.TerritoryType;
        if (territory != target.IssuerTerritoryId)
            return $"current territory {territory} is not issuer territory {target.IssuerTerritoryId}.";

        var player = Player.Object;
        if (player == null)
            return "local player is not available.";

        var nearby = new List<string>(8);
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == nint.Zero || obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc)
                continue;

            var distance = Vector3.Distance(player.Position, obj.Position);
            if (distance > 40f)
                continue;

            var identity = ReadObjectIdentity(obj);
            nearby.Add($"{identity.DataId}/{identity.BaseId} '{obj.Name.TextValue}' {distance:F1}y targetable={obj.IsTargetable}");
            if (nearby.Count >= 8)
                break;
        }

        return nearby.Count == 0 ? "no EventNpc within 40y." : string.Join("; ", nearby);
    }

    private IReadOnlyList<string> AssignmentCategoryLabels
    {
        get
        {
            if (_assignmentCategoryLabels != null)
                return _assignmentCategoryLabels;

            _assignmentCategoryLabels = Service.DataManager.GetExcelSheet<LeveAssignmentType>()
                .Select(row => row.Name.ExtractText())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return _assignmentCategoryLabels;
        }
    }

    private static (uint DataId, uint BaseId) ReadObjectIdentity(IGameObject obj)
    {
#pragma warning disable CS0618 // DataId is the previous name of BaseId on Dalamud API 15. Compare both until a client shows they differ.
        return (obj.DataId, obj.BaseId);
#pragma warning restore CS0618
    }

    private static LeveIssuerSelection.MenuEntry[] ReadSelectString(AddonMaster.SelectString master)
        => master.Entries.Select(entry => new LeveIssuerSelection.MenuEntry(entry.Index, entry.Text ?? string.Empty)).ToArray();

    private void DriveLeveIssuerMenu(AddonMaster.SelectString master)
    {
        if (master.Entries.Length == 0 || !EzThrottler.Throttle("ZBR_LeveIssuerMenu", 700))
            return;

        var target = _leveContext.AutomationTarget;
        var entries = ReadSelectString(master);
        var rendered = string.Join(" | ", entries.Select(entry => $"{entry.Index}:'{entry.Text}'"));
        var index = LeveIssuerSelection.ChooseAcquire(target.AssignmentTypeId, target.AssignmentLabel, entries, out var detail);
        if (EzThrottler.Throttle("ZBR_LeveIssuerMenuLog", 5000))
        {
            Service.PluginLog.Information(
                $"[ZodiacBuddy/LEVE] LeveId={target.LeveId} state={_leveContext.AutomationState} assignment={target.AssignmentTypeId} '{target.AssignmentLabel}' menu: {rendered}. {detail}");
        }

        if (index == null)
        {
            FailLeveAutomation($"Unknown acquisition menu for LeveId={target.LeveId} assignment={target.AssignmentTypeId} '{target.AssignmentLabel}'. {detail} Entries: {rendered}");
            return;
        }

        master.Entries.First(entry => entry.Index == index.Value).Select();
    }
    private static string GetLeveIssuerName(string issuer)
    {
        var suffix = issuer.IndexOf(" (", StringComparison.Ordinal);
        return suffix < 0 ? issuer : issuer[..suffix];
    }
    private static unsafe bool IsLeveAccepted(uint leveId, out FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions.LeveWork work)
    {
        work = default;
        var questManager = QuestManager.Instance();
        if (questManager == null)
            return false;

        foreach (var candidate in questManager->LeveQuests.ToArray())
        {
            if (candidate.LeveId != leveId)
                continue;
            work = candidate;
            return true;
        }
        return false;
    }
    private unsafe bool TryExitLeveIssuerUi(string reason)
    {
        if (TryDriveLeveTalk())
            return true;

        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("JournalDetail", out var journalDetail) && journalDetail != null && GenericHelpers.IsAddonReady(journalDetail))
        {
            journalDetail->Close(true);
            return true;
        }

        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("GuildLeve", out var guildLeve) && guildLeve != null && GenericHelpers.IsAddonReady(guildLeve))
        {
            guildLeve->Close(true);
            return true;
        }

        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectString", out var selectString) || selectString == null || !GenericHelpers.IsAddonReady(selectString))
            return false;

        var master = new AddonMaster.SelectString(selectString);
        if (master.Entries.Length == 0)
            return true;
        if (!EzThrottler.Throttle("ZBR_LeveExitIssuerMenu", 700))
            return true;

        var entries = ReadSelectString(master);
        var rendered = string.Join(" | ", entries.Select(entry => $"{entry.Index}:'{entry.Text}'"));
        var index = LeveIssuerSelection.ChooseExit(AssignmentCategoryLabels, entries, out var detail);
        if (EzThrottler.Throttle("ZBR_LeveExitIssuerMenuLog", 5000))
            Service.PluginLog.Information($"[ZodiacBuddy/LEVE] Exit menu while {reason} for LeveId={_leveContext.AutomationTarget.LeveId}: {rendered}. {detail}");
        if (index == null)
        {
            FailLeveAutomation($"Cannot close the issuer menu while {reason} for LeveId={_leveContext.AutomationTarget.LeveId}. {detail} Entries: {rendered}");
            return true;
        }

        master.Entries.First(entry => entry.Index == index.Value).Select();
        return true;
    }
    private void BeginLeveUnavailableClose(string message)
    {
        _leveContext.AutomationState = LeveAutomationState.UnavailableClosing;
        _leveContext.AutomationStatus = message;
        _leveContext.AcquireCompletedAt = DateTime.Now;
        Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] {message} Returning control to the book planner after closing issuer UI.");
    }
    private void DriveLeveUnavailableClose()
    {
        if (TryExitLeveIssuerUi("after unavailable offering"))
            return;
        if ((DateTime.Now - _leveContext.AcquireCompletedAt).TotalMilliseconds < LeveIssuerExitSettleMs)
            return;

        StopLeveRotationSolver("leve offering was unavailable");
        _automationRun.ReplaceNavigation();
        ClearLeveNavigationResult();
        Svc.Framework.Update -= ObserveLeveAutomation;
        _automationRun.Cancel();
        _leveContext.AutomationRunId = 0;
        _leveContext.AutomationState = LeveAutomationState.Unavailable;
        Service.PluginLog.Verbose($"[ZodiacBuddy/LEVE] LeveId={_leveContext.AutomationTarget.LeveId} returned as temporarily unavailable to the book planner.");
    }
}
