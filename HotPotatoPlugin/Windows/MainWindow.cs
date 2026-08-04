using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using HotPotatoPlugin.Services;

namespace HotPotatoPlugin.Windows;

public sealed class MainWindow : Window
{
    private readonly GameManager gameManager;
    private readonly IPartyList partyList;
    private readonly IObjectTable objectTable;
    private readonly IChatGui chatGui;
    private readonly PartyChatService partyChatService;
    private IReadOnlyList<int> newestNumbers = Array.Empty<int>();
    private string playerName = string.Empty;
    private string statusMessage = string.Empty;
    private int rollLimit = 999;
    private int entryFeeInput = 100_000;
    private float houseCutPercent = 30f;
    private Guid? pendingRemovalPlayerId;
    private int startingPotatoCount = 25;
    private int potatoesAddedPerRound = 5;

    public MainWindow(
        GameManager gameManager,
        PartyChatService partyChatService,
        IPartyList partyList,
        IObjectTable objectTable,
        IChatGui chatGui)
        : base("Hot Potato Game Manager")
    {
        this.gameManager = gameManager;
        this.partyChatService = partyChatService;
        this.partyList = partyList;
        this.objectTable = objectTable;
        this.chatGui = chatGui;

        this.chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
    }

    public override void Draw()
    {
        DrawStatus();

        ImGui.Separator();
        ImGui.Spacing();

        if (gameManager.IsGameRunning)
        {
            DrawRunningGame();
        }
        else
        {
            DrawGameSetup();
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped(statusMessage);
        }
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        if (chatMessage.LogKind != XivChatType.Party
            && chatMessage.LogKind != XivChatType.RandomNumber)
        {
            return;
        }

        if (!gameManager.IsGameRunning
            || gameManager.IsGameComplete)
        {
            return;
        }

        var senderText = chatMessage.Sender.TextValue.Trim();
        var messageText = chatMessage.Message.TextValue.Trim();

        var rollMatch = Regex.Match(
            messageText,
            @"^random!\s*(?:\(\s*(\d+)\s*-\s*(\d+)\s*\)\s*)?(\d+)\s*$",
            RegexOptions.IgnoreCase);

        if (!rollMatch.Success)
        {
            return;
        }

        if (!int.TryParse(
                rollMatch.Groups[3].Value,
                out var roll))
        {
            return;
        }

        var senderName = Regex.Replace(
            senderText,
            @"^[^\p{L}]+",
            string.Empty).Trim();

        if (rollMatch.Groups[1].Success
            && rollMatch.Groups[2].Success)
        {
            if (!int.TryParse(
                    rollMatch.Groups[1].Value,
                    out var messageMinimum)
                || !int.TryParse(
                    rollMatch.Groups[2].Value,
                    out var messageMaximum))
            {
                return;
            }

            if (messageMinimum != 1
                || messageMaximum != gameManager.Settings.MaximumNumber)
            {
                statusMessage =
                    $"{senderName} used the wrong dice limit. "
                    + $"Expected {gameManager.Settings.MaximumNumber}, "
                    + $"but they used {messageMaximum}.";

                return;
            }
        }
            else if (gameManager.Settings.MaximumNumber != 999)
            {
                statusMessage =
                    $"{senderName} used the standard dice limit of 999. "
                    + $"Expected "
                    + $"{gameManager.Settings.MaximumNumber}.";

                return;
            }

            var player = gameManager.Players
                .OrderByDescending(currentPlayer => currentPlayer.Name.Length)
                .FirstOrDefault(currentPlayer =>
                    string.Equals(
                        currentPlayer.Name,
                        senderName,
                        StringComparison.OrdinalIgnoreCase)
                    || senderName.StartsWith(
                        currentPlayer.Name,
                        StringComparison.OrdinalIgnoreCase));

        if (player is null)
        {
            statusMessage =
                $"Ignored roll from {senderName}: "
                + "they are not a participant.";

            return;
        }

        if (player.IsEliminated)
        {
            statusMessage =
                $"Ignored roll from {player.Name}: "
                + "they have already been eliminated.";

            return;
        }

        if (gameManager.CurrentPlayerId != player.Id)
        {
            statusMessage =
                $"Ignored roll from {player.Name}: "
                + $"it is currently "
                + $"{gameManager.CurrentPlayer?.Name}'s turn.";

            return;
        }

        var numbersBeforeRoll =
            gameManager.HotPotatoNumbers.ToHashSet();

        if (!gameManager.ProcessRoll(
                player.Id,
                roll,
                out var isHotPotato))
        {
            statusMessage =
                $"Could not process {senderName}'s roll of {roll}.";

            return;
        }

        var newlyAddedNumbers = gameManager.HotPotatoNumbers
            .Where(number => !numbersBeforeRoll.Contains(number))
            .ToList();

        if (gameManager.IsGameComplete
            && gameManager.Winner is not null)
        {
            statusMessage =
                $"{player.Name} rolled {roll}. HOT POTATO! "
                + $"{gameManager.Winner.Name} wins "
                + $"{gameManager.WinnerPot:N0} gil!";

            partyChatService.QueueMessage(
                $"{player.Name} rolled {roll}. HOT POTATO!");

            partyChatService.QueueMessage(
                $"{gameManager.Winner.Name} wins Hot Potato!");

            partyChatService.QueueMessage(
                $"{gameManager.Winner.Name} receives "
                + $"{gameManager.WinnerPot:N0} gil! ");

            return;
        }

        if (isHotPotato)
        {
            statusMessage =
                $"{player.Name} rolled {roll}. HOT POTATO!\n"
                + $"They were eliminated. Round "
                + $"{gameManager.CurrentRound} started.\n"
                + $"{gameManager.CurrentPlayer?.Name} rolls next.";

            partyChatService.QueueMessage(
                $"{player.Name} rolled {roll}. HOT POTATO! "
                + "They have been eliminated! <se.6>");

            partyChatService.QueueMessage(
                $"Round {gameManager.CurrentRound} is starting.");

            partyChatService.QueueNumberList(
                "New Hot Potato numbers:",
                newlyAddedNumbers);

            if (gameManager.CurrentPlayer is not null)
            {
                partyChatService.QueueMessage(
                    $"{gameManager.CurrentPlayer.Name} rolls next!");
            }
        }
        else
        {
            statusMessage =
                $"{player.Name} rolled {roll}. Safe.\n"
                + $"{gameManager.CurrentPlayer?.Name} rolls next.";

            partyChatService.QueueMessage(
                $"{player.Name} rolled {roll}. Safe!");

            if (gameManager.CurrentPlayer is not null)
            {
                partyChatService.QueueMessage(
                    $"{gameManager.CurrentPlayer.Name} rolls next!");
            }
        }
    }

    private void ImportPartyMembers()
    {
        if (gameManager.IsGameRunning)
        {
            statusMessage =
                "You cannot import party members while a game is running.";

            return;
        }

        var localPlayer = objectTable.LocalPlayer;

        if (localPlayer is null)
        {
            statusMessage =
                "Your character information is not currently available.";

            return;
        }

        var localPlayerName = localPlayer.Name.TextValue;

        var importedCount = 0;
        var skippedCount = 0;

        foreach (var partyMember in partyList)
        {
            var memberName = partyMember.Name.TextValue;

            if (string.IsNullOrWhiteSpace(memberName))
            {
                continue;
            }

            // The local player is the host, not a participant.
            if (string.Equals(
                    memberName,
                    localPlayerName,
                    StringComparison.OrdinalIgnoreCase))
            {
                skippedCount++;
                continue;
            }

            var alreadyExists = gameManager.Players.Any(
                player => string.Equals(
                    player.Name,
                    memberName,
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                skippedCount++;
                continue;
            }

            if (gameManager.AddPlayer(memberName))
            {
                importedCount++;
            }
        }

        statusMessage = importedCount > 0
            ? $"Imported {importedCount} party member(s)."
            : "No new party members were found.";

        if (skippedCount > 0)
        {
            statusMessage +=
                $" Skipped {skippedCount} host or duplicate member(s).";
        }
    }

    private void DrawStatus()
    {
        ImGui.Text("Game Status");

        if (gameManager.IsGameComplete
            && gameManager.Winner is not null)
        {
            ImGui.Text("Game complete");
        }
        else if (gameManager.IsGameRunning)
        {
            ImGui.Text("Game in progress");
        }
        else
        {
            ImGui.Text("No game running");
        }

        ImGui.Text($"Players: {gameManager.Players.Count}");
    }

    private void DrawGameSetup()
    {
        ImGui.Text("Players");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(275);

        var submittedWithEnter = ImGui.InputText(
            "##PlayerName",
            ref playerName,
            50,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();

        var addButtonClicked = ImGui.Button("Add Player");

        if (submittedWithEnter || addButtonClicked)
        {
            AddPlayer();
        }

        if (ImGui.Button("Import Current Party"))
        {
            ImportPartyMembers();
        }

        ImGui.Spacing();

        if (gameManager.Players.Count == 0)
        {
            ImGui.TextDisabled("No players have been added.");
        }
        else
        {
            foreach (var player in gameManager.Players.ToList())
            {
                ImGui.Text(player.Name);
                ImGui.SameLine();

                if (ImGui.SmallButton($"Remove##{player.Id}"))
                {
                    gameManager.RemovePlayer(player.Id);
                    statusMessage = $"{player.Name} was removed.";
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawRollSettings();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPotSettings();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPotPreview();

        ImGui.Spacing();

        DrawStartGameButton();
    }

    private void DrawRollSettings()
    {
        ImGui.Text("Game Settings");

        ImGui.SetNextItemWidth(180);
        ImGui.InputInt(
            "Roll Limit",
            ref rollLimit,
            50,
            100);

        rollLimit = Math.Max(1, rollLimit);

        ImGui.TextDisabled(
            $"Players will use: /dice party {rollLimit}");

        ImGui.Spacing();

        ImGui.SetNextItemWidth(180);
        ImGui.InputInt(
            "Starting Hot Potato Numbers",
            ref startingPotatoCount,
            1,
            5);

        startingPotatoCount = Math.Max(
            1,
            startingPotatoCount);

        ImGui.SetNextItemWidth(180);
        ImGui.InputInt(
            "Numbers Added After Elimination",
            ref potatoesAddedPerRound,
            1,
            5);

        potatoesAddedPerRound = Math.Max(
            1,
            potatoesAddedPerRound);
    }

    private void DrawPotSettings()
    {
        ImGui.Text("Pot Settings");

        ImGui.SetNextItemWidth(180);
        ImGui.InputInt(
            "Entry Fee Per Player",
            ref entryFeeInput,
            10_000,
            100_000);

        entryFeeInput = Math.Max(
            0,
            entryFeeInput);

        ImGui.SetNextItemWidth(180);
        ImGui.InputFloat(
            "House Cut %",
            ref houseCutPercent,
            1f,
            5f,
            "%.1f");

        houseCutPercent = Math.Clamp(
            houseCutPercent,
            0f,
            100f);
    }

    private void DrawPotPreview()
    {
        var previewGrossPot =
            (long)gameManager.Players.Count
            * entryFeeInput;

        var previewHouseCut = (long)Math.Round(
            previewGrossPot
            * (houseCutPercent / 100f),
            MidpointRounding.AwayFromZero);

        var previewWinnerPot =
            previewGrossPot - previewHouseCut;

        ImGui.Text("Pot Preview");
        ImGui.Text(
            $"Players: {gameManager.Players.Count}");

        ImGui.Text(
            $"Entry Fee: {entryFeeInput:N0} gil");

        ImGui.Text(
            $"Total Pot: {previewGrossPot:N0} gil");

        ImGui.Text(
            $"House Cut ({houseCutPercent:0.#}%): "
            + $"{previewHouseCut:N0} gil");

        ImGui.Text(
            $"Winner Receives: {previewWinnerPot:N0} gil");
    }

    private void DrawStartGameButton()
    {
        if (!ImGui.Button("Start Game"))
        {
            return;
        }

        if (rollLimit < 1)
        {
            statusMessage =
                "Enter a valid roll limit.";

            return;
        }

        if (startingPotatoCount > rollLimit)
        {
            statusMessage =
                "Starting Hot Potato numbers cannot exceed the roll limit.";

            return;
        }

        if (potatoesAddedPerRound < 1)
        {
            statusMessage =
                "Numbers added after an elimination must be at least 1.";

            return;
        }

        var rangeSize = rollLimit;

        if (rangeSize
            < gameManager.Settings.InitialNumberCount)
        {
            statusMessage =
                $"The roll range must contain at least "
                + $"{gameManager.Settings.InitialNumberCount} numbers.";

            return;
        }

        gameManager.Settings.MaximumNumber = 
            rollLimit;
        
        gameManager.Settings.InitialNumberCount =
            startingPotatoCount;

        gameManager.Settings.NumbersPerRound =
            potatoesAddedPerRound;

        gameManager.Settings.EntryFee =
            entryFeeInput;

        gameManager.Settings.HouseCutPercent =
            houseCutPercent;

        if (!gameManager.StartGame())
        {
            statusMessage =
                "Add at least two players and enter valid game settings.";

            return;
        }

        newestNumbers =
            gameManager.HotPotatoNumbers.ToList();

        statusMessage =
            $"Round 1 started with "
            + $"{newestNumbers.Count} Hot Potato numbers "
            + $"using a roll limit of {rollLimit}. "
            + $"Winner payout: "
            + $"{gameManager.WinnerPot:N0} gil.";

        partyChatService.QueueMessage(
            "=== HOT POTATO ===\n"
            + "HOW TO PLAY:\n"
            + "- Dealer announces the starting Hot Potato numbers.\n"
            + $"- On your turn use /dice party {rollLimit}.\n"
            + "- Avoid the announced Hot Potato numbers.\n"
            + "- Roll one and you're OUT!\n"
            + $"- {potatoesAddedPerRound} new Hot Potato numbers are added after each elimination.\n"
            + "- Last player standing wins!");

        partyChatService.QueueMessage(
            $"Hot Potato is starting! "
            + $"{gameManager.StartingPlayerCount} players. "
            + $"Use /dice party {rollLimit}.");

        partyChatService.QueueMessage(
            $"Starting with {startingPotatoCount} Hot Potato numbers. "
            + $"{potatoesAddedPerRound} new numbers will be added "
            + "after each elimination.");

        partyChatService.QueueMessage(
            $"Entry: "
            + $"{gameManager.Settings.EntryFee:N0} gil per player. "
            + $"Total pot: "
            + $"{gameManager.GrossPot:N0} gil.");

        partyChatService.QueueMessage(
             $"Winner receives: "
            + $"{gameManager.WinnerPot:N0} gil.");

        partyChatService.QueueNumberList(
            "Starting Hot Potato numbers:",
            newestNumbers);

        if (gameManager.CurrentPlayer is not null)
        {
            partyChatService.QueueMessage(
                $"{gameManager.CurrentPlayer.Name} rolls first!");
        }
    }

    private void DrawRunningGame()
    {
        ImGui.Text(
            $"Round {gameManager.CurrentRound}");

        ImGui.Text(
            $"Active Players: "
            + $"{gameManager.ActivePlayers.Count}");

        ImGui.Text(
            $"Total Hot Potato Numbers: "
            + $"{gameManager.HotPotatoNumbers.Count}");

        ImGui.Spacing();

        ImGui.Text("Game Pot");

        ImGui.Text(
            $"Entry Fee: "
            + $"{gameManager.Settings.EntryFee:N0} gil");

        ImGui.Text(
            $"Total Pot: "
            + $"{gameManager.GrossPot:N0} gil");

        ImGui.Text(
            $"House Cut: "
            + $"{gameManager.HouseCutAmount:N0} gil "
            + $"({gameManager.Settings.HouseCutPercent:0.#}%)");

        ImGui.Text(
            $"Winner Receives: "
            + $"{gameManager.WinnerPot:N0} gil");

        if (gameManager.CurrentPlayer is not null)
        {
            ImGui.Spacing();

            ImGui.Text(
                $"Current Roller: "
                + $"{gameManager.CurrentPlayer.Name}");
        }

        if (gameManager.IsGameComplete
            && gameManager.Winner is not null)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text(
                $"WINNER: {gameManager.Winner.Name}");

            ImGui.Text(
                $"PAYOUT: "
                + $"{gameManager.WinnerPot:N0} gil");

            ImGui.Spacing();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Hot Potato Numbers");
        ImGui.Spacing();

        DrawNumberGrid(
            gameManager.HotPotatoNumbers);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPlayerList();

        ImGui.Spacing();

        if (ImGui.Button("Reset Game"))
        {
            partyChatService.ClearQueue();
            gameManager.ResetGame();

            newestNumbers = Array.Empty<int>();
            pendingRemovalPlayerId = null;

            statusMessage =
                "The game was reset.";
        }
    }

    private void DrawPlayerList()
    {
        ImGui.Text("Players");
        ImGui.Spacing();

        foreach (var player in gameManager.Players)
        {
            var playerLabel = player.Name;

            if (player.IsRemoved)
            {
                playerLabel += " - Removed";
            }
            else if (player.IsEliminated)
            {
                playerLabel += " - Eliminated";
            }

            ImGui.Text(playerLabel);

            if (player.LastRoll.HasValue)
            {
                ImGui.SameLine();

                ImGui.TextDisabled(
                    $"Last roll: {player.LastRoll.Value}");
            }

            if (gameManager.IsGameRunning
                && !gameManager.IsGameComplete
                && player.IsActive)
            {
                ImGui.SameLine();

                if (ImGui.SmallButton(
                        $"Remove##{player.Id}"))
                {
                    pendingRemovalPlayerId = player.Id;

                    ImGui.OpenPopup(
                        "Confirm Player Removal");
                }
            }
        }

        DrawRemovePlayerConfirmation();
    }

    private void DrawRemovePlayerConfirmation()
    {
        if (!ImGui.BeginPopupModal(
                "Confirm Player Removal",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        var player = pendingRemovalPlayerId.HasValue
            ? gameManager.Players.FirstOrDefault(
                currentPlayer =>
                    currentPlayer.Id
                    == pendingRemovalPlayerId.Value)
            : null;

        if (player is null)
        {
            ImGui.Text(
                "The selected player could not be found.");

            if (ImGui.Button("Close"))
            {
                pendingRemovalPlayerId = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
            return;
        }

        ImGui.Text(
            $"Remove {player.Name} from the game?");

        ImGui.Spacing();

        ImGui.TextWrapped(
            "Their entry fee will be removed from the pot, "
            + "and they will no longer participate.");

        ImGui.Spacing();

        if (ImGui.Button("Cancel"))
        {
            pendingRemovalPlayerId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button("Remove Player"))
        {
            RemovePlayerDuringGame(player.Id);

            pendingRemovalPlayerId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }
    private void RemovePlayerDuringGame(Guid playerId)
    {
        if (!gameManager.RemovePlayerDuringGame(
                playerId,
                out var removedPlayer,
                out var nextPlayer)
            || removedPlayer is null)
        {
            statusMessage =
                "That player could not be removed.";

            return;
        }

        statusMessage =
            $"{removedPlayer.Name} was removed. "
            + $"Winner payout is now "
            + $"{gameManager.WinnerPot:N0} gil.";

        partyChatService.QueueMessage(
            $"{removedPlayer.Name} has been removed "
            + "from the game.");

        partyChatService.QueueMessage(
            $"Winner payout is now "
            + $"{gameManager.WinnerPot:N0} gil.");

        if (gameManager.IsGameComplete
            && gameManager.Winner is not null)
        {
            statusMessage =
                $"{removedPlayer.Name} was removed. "
                + $"{gameManager.Winner.Name} wins "
                + $"{gameManager.WinnerPot:N0} gil!";

            partyChatService.QueueMessage(
                $"{gameManager.Winner.Name} "
                + "is the last remaining player!");

            partyChatService.QueueMessage(
                $"{gameManager.Winner.Name} receives "
                + $"{gameManager.WinnerPot:N0} gil!");

            return;
        }

        if (nextPlayer is not null)
        {
            partyChatService.QueueMessage(
                $"{nextPlayer.Name} rolls next!");
        }
    }
    private static void DrawNumberGrid(
        IReadOnlyList<int> numbers)
    {
        const int columns = 5;

        if (!ImGui.BeginTable(
                "HotPotatoNumberTable",
                columns,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        foreach (var number in numbers)
        {
            ImGui.TableNextColumn();
            ImGui.Text(number.ToString());
        }

        ImGui.EndTable();
    }

    private void AddPlayer()
    {
        var nameBeingAdded =
            playerName.Trim();

        if (gameManager.AddPlayer(
                nameBeingAdded))
        {
            statusMessage =
                $"{nameBeingAdded} was added.";

            playerName =
                string.Empty;

            return;
        }

        statusMessage =
            "Enter a unique player name "
            + "before adding a player.";
    }
}