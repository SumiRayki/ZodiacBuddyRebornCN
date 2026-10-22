using System;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ZodiacBuddy.BonusLight;
using ZodiacBuddy.Stages.Animus;
using ZodiacBuddy.Stages.Brave;
using ZodiacBuddy.Stages.Novus;
using ECommons;
using ECommons.DalamudServices;

namespace ZodiacBuddy;

/// <summary>
/// Main plugin implementation.
/// </summary>
public sealed class ZodiacBuddyPlugin : IDalamudPlugin {
    private const string Command = "/pzodiac";
    private const string TargetWindowCommand = "/ztarget";
    private const string CnCommand = "/pzodiac-cn";
    private const string CnTargetWindowCommand = "/ztarget-cn";
    private static readonly TimeSpan SharedCommandDelay = TimeSpan.FromSeconds(3);
#if DEBUG
    private const string FateDebugCommand = "/zfate";
    private const string LeveDebugCommand = "/zleve";
    private const string FateGrinderCommand = "/zgrind";
#endif

    private readonly NovusManager novusManager;
    private readonly BraveManager braveManager;
    private readonly WindowSystem windowSystem;
    internal TargetInfoWindow TargetWindow;

    private readonly ConfigWindow configWindow;
    private readonly AnimusManager animus;
    private readonly AnimusAutomationFacade automation;
    private readonly DateTime loadedAtUtc = DateTime.UtcNow;
    private bool sharedCommandsRegistered;
#if DEBUG
    private readonly FateDebugWindow fateDebugWindow;
    private readonly FateGrinderWindow fateGrinderWindow;
    private readonly FateGrinderMultiPullWindow fateGrinderMultiPullWindow;
    private readonly LeveDebugWindow leveDebugWindow;
#endif
    /// <summary>
    /// Initializes a new instance of the <see cref="ZodiacBuddyPlugin"/> class.
    /// </summary>
    /// <param name="pluginInterface">Dalamud plugin interface.</param>
    public ZodiacBuddyPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();

        ECommons.ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        Service.Plugin = this;
        Service.Configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();

        this.windowSystem = new WindowSystem("ZodiacBuddyRebornCN");

        this.windowSystem.AddWindow(this.configWindow = new ConfigWindow());
        Service.Interface.UiBuilder.OpenConfigUi += this.OnOpenConfigUi;

        Service.CommandManager.AddHandler(CnCommand, new CommandInfo(this.OnCommand) {
            HelpMessage = "打开国服适配版设置窗口。原版与本插件不能同时启用。",
            ShowInHelp = true,
        });
        Service.CommandManager.AddHandler(CnTargetWindowCommand, new CommandInfo(OnTargetWindowCommand)
        {
            HelpMessage = "打开国服适配版目标窗口。原版与本插件不能同时启用。",
            ShowInHelp = true,
        });
        Service.Framework.Update += this.WatchUpstreamConflict;

        Service.BonusLightManager = new BonusLightManager();
        this.novusManager = new NovusManager();
        this.braveManager = new BraveManager();
        this.animus = new AnimusManager();
        this.automation = new AnimusAutomationFacade(animus);
        TargetWindow = new TargetInfoWindow(automation);
        windowSystem.AddWindow(TargetWindow);
#if DEBUG
        this.windowSystem.AddWindow(this.fateDebugWindow = new FateDebugWindow(automation));
        Service.CommandManager.AddHandler(FateDebugCommand, new CommandInfo(OnFateDebugCommand)
        {
            HelpMessage = "Open the Animus FATE automation test harness.",
            ShowInHelp = true,
        });
        this.windowSystem.AddWindow(this.fateGrinderMultiPullWindow = new FateGrinderMultiPullWindow());
        this.windowSystem.AddWindow(this.fateGrinderWindow = new FateGrinderWindow(automation, fateGrinderMultiPullWindow));
        Service.CommandManager.AddHandler(FateGrinderCommand, new CommandInfo(OnFateGrinderCommand)
        {
            HelpMessage = "Open the ZodiacBuddy FATE grinder.",
            ShowInHelp = true,
        });
        this.windowSystem.AddWindow(this.leveDebugWindow = new LeveDebugWindow(automation));
        Service.CommandManager.AddHandler(LeveDebugCommand, new CommandInfo(OnLeveDebugCommand)
        {
            HelpMessage = "Open the Animus leve test harness. Optional: /zleve <LeveId>.",
            ShowInHelp = true,
        });
#endif
        AutoDutyIpc.Init();
        RSRIPC.Init();
        BossModIPC.Init();
        Service.Interface.UiBuilder.Draw += this.windowSystem.Draw;
    }

    /// <inheritdoc/>
    public void Dispose() {
        Service.Framework.Update -= this.WatchUpstreamConflict;
        Service.Interface.UiBuilder.Draw -= this.windowSystem.Draw;
        Svc.Framework.Update -= animus.WaitForBetweenAreasAndExecute;
        animus.Dispose();
        Service.CommandManager.RemoveHandler(CnCommand);
        Service.CommandManager.RemoveHandler(CnTargetWindowCommand);
        this.UnregisterSharedCommands();
        TargetWindow.Dispose();
        windowSystem.RemoveWindow(TargetWindow);
#if DEBUG
        Service.CommandManager.RemoveHandler(FateDebugCommand);
        windowSystem.RemoveWindow(fateDebugWindow);
        Service.CommandManager.RemoveHandler(FateGrinderCommand);
        windowSystem.RemoveWindow(fateGrinderWindow);
        windowSystem.RemoveWindow(fateGrinderMultiPullWindow);
        Service.CommandManager.RemoveHandler(LeveDebugCommand);
        windowSystem.RemoveWindow(leveDebugWindow);
#endif
        Service.Interface.UiBuilder.OpenConfigUi -= this.OnOpenConfigUi;

        this.novusManager.Dispose();
        this.braveManager.Dispose();
        Service.BonusLightManager.Dispose();
        ECommons.ECommonsMain.Dispose();
    }
#if DEBUG
    internal void OpenFateGrinderWindow()
        => fateGrinderWindow.IsOpen = true;

    internal void OpenFateDebugWindow()
        => fateDebugWindow.IsOpen = true;

    private void OnFateDebugCommand(string command, string arguments)
        => fateDebugWindow.IsOpen = true;

    private void OnLeveDebugCommand(string command, string arguments)
    {
        if (uint.TryParse(arguments.Trim(), out var leveId))
            leveDebugWindow.SelectLeve(leveId);
        leveDebugWindow.IsOpen = true;
    }

    private void OnFateGrinderCommand(string command, string arguments)
        => fateGrinderWindow.IsOpen = true;
#endif

    private void WatchUpstreamConflict(IFramework framework)
    {
        if (UpstreamConflictGuard.IsLoaded())
        {
            if (!Service.UpstreamConflict)
            {
                Service.UpstreamConflict = true;
                this.UnregisterSharedCommands();
                PrintError("检测到原版 ZodiacBuddy Reborn。国服适配版不能与原版同时启用，已停止本插件的文书自动化，并取消共用命令 /pzodiac 与 /ztarget。请在卫月里只保留其中一个插件。/pzodiac-cn 与 /ztarget-cn 仍可打开本插件窗口。");
            }

            return;
        }

        if (this.sharedCommandsRegistered || DateTime.UtcNow - this.loadedAtUtc < SharedCommandDelay)
            return;

        try
        {
            this.RegisterSharedCommands();
        }
        catch (Exception ex)
        {
            Service.UpstreamConflict = true;
            this.UnregisterSharedCommands();
            Service.PluginLog.Error(ex, "[ZodiacBuddyRebornCN] Shared commands are already owned. The upstream plugin and this CN adaptation cannot run together.");
            PrintError("共用命令 /pzodiac 或 /ztarget 已被占用。请只启用原版或国服适配版其中一个。本插件已停止文书自动化。");
        }
    }

    private void RegisterSharedCommands()
    {
        if (this.sharedCommandsRegistered || Service.UpstreamConflict)
            return;

        Service.CommandManager.AddHandler(Command, new CommandInfo(this.OnCommand) {
            HelpMessage = "Open settings for the unofficial CN adaptation. Do not enable the upstream plugin at the same time.",
            ShowInHelp = true,
        });
        try
        {
            Service.CommandManager.AddHandler(TargetWindowCommand, new CommandInfo(this.OnTargetWindowCommand)
            {
                HelpMessage = "Open the CN adaptation target window. Do not enable the upstream plugin at the same time.",
                ShowInHelp = true,
            });
        }
        catch
        {
            Service.CommandManager.RemoveHandler(Command);
            throw;
        }

        this.sharedCommandsRegistered = true;
    }

    private void UnregisterSharedCommands()
    {
        if (!this.sharedCommandsRegistered)
            return;

        Service.CommandManager.RemoveHandler(Command);
        Service.CommandManager.RemoveHandler(TargetWindowCommand);
        this.sharedCommandsRegistered = false;
    }

    private void OnTargetWindowCommand(string command, string arguments)
    {
        TargetWindow.IsOpen = true;
    }
    /// <summary>
    /// Print a message.
    /// </summary>
    /// <param name="message">Message to send.</param>
    public void PrintMessage(SeString message) {
        var sb = new SeStringBuilder()
            .AddUiForeground("[ZodiacBuddyRebornCN] ", 45)
            .Append(message);

        Service.ChatGui.Print(new XivChatEntry {
            Type = Service.Configuration.ChatType,
            Message = sb.BuiltString,
        });
    }

    public void PrintStepProgress(string message)
    {
        if (Service.Configuration.BraveEchoTarget)
            PrintMessage(message);
    }

    public void PrintStepProgress(SeString message)
    {
        if (Service.Configuration.BraveEchoTarget)
            PrintMessage(message);
    }

    /// <summary>
    /// Print an error message.
    /// </summary>
    /// <param name="message">Message to send.</param>
    public static void PrintError(string message)
        => Service.ChatGui.PrintError($"[ZodiacBuddyRebornCN] {message}");
    private void OnOpenConfigUi()
        => this.configWindow.IsOpen = true;

    private void OnCommand(string command, string arguments)
        => this.configWindow.IsOpen = true;
}
