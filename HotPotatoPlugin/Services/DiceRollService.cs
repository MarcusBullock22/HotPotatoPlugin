using System;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace HotPotatoPlugin.Services;

public unsafe sealed class DiceRollService : IDisposable
{
    private readonly IGameInteropProvider gameInteropProvider;
    private readonly IPluginLog log;

    public event Action<string, int, int>? OnRollReceived;

    [Signature(
        "48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B7 BC 24",
        DetourName = nameof(DicePrintLogDetour))]
    private Hook<DicePrintLogDelegate> dicePrintLogHook = null!;

    private delegate void DicePrintLogDelegate(
        RaptureLogModule* module,
        ushort chatType,
        byte* playerName,
        void* unused,
        ushort worldId,
        ulong accountId,
        ulong contentId,
        ushort roll,
        ushort outOf,
        uint entityId,
        byte ident);

    public DiceRollService(IGameInteropProvider gameInteropProvider, IPluginLog log)
    {
        this.gameInteropProvider = gameInteropProvider;
        this.log = log;

        this.gameInteropProvider.InitializeFromAttributes(this);

        dicePrintLogHook.Enable();
    }

    private void DicePrintLogDetour(
        RaptureLogModule* module,
        ushort chatType,
        byte* playerName,
        void* unused,
        ushort worldId,
        ulong accountId,
        ulong contentId,
        ushort roll,
        ushort outOf,
        uint entityId,
        byte ident)
    {
        try
        {
            var name =
                MemoryHelper.ReadStringNullTerminated(
                    (nint)playerName);

            OnRollReceived?.Invoke(
                name,
                roll,
                outOf);
        }
        catch (Exception exception)
        {
            log.Error(
                exception,
                "Unable to process dice roll.");
        }

        dicePrintLogHook.Original(
            module,
            chatType,
            playerName,
            unused,
            worldId,
            accountId,
            contentId,
            roll,
            outOf,
            entityId,
            ident);
    }

    public void Dispose()
    {
        dicePrintLogHook.Dispose();
    }
}