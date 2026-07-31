using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HotPotatoPlugin.Services;
using HotPotatoPlugin.Windows;

namespace HotPotatoPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IPartyList PartyList { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/hotpotato";

    private readonly WindowSystem windowSystem = new("HotPotatoPlugin");
    private readonly GameManager gameManager;
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        gameManager = new GameManager();
        mainWindow = new MainWindow(
            gameManager,
            PartyList,
            ObjectTable,
            ChatGui);

        windowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the Hot Potato game manager."
            });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information(
            $"Loaded {PluginInterface.Manifest.Name}.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        mainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string args)
    {
        mainWindow.Toggle();
    }

    private void ToggleMainUi()
    {
        mainWindow.Toggle();
    }
}