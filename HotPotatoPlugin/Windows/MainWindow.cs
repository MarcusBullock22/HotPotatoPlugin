using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HotPotatoPlugin.Services;
using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using System.Text.RegularExpressions;

namespace HotPotatoPlugin.Windows;

public sealed class MainWindow : Window
{
    private readonly GameManager gameManager;
    private IReadOnlyList<int> newestNumbers = Array.Empty<int>();
    private string playerName = string.Empty;
    private string statusMessage = string.Empty;
    private Guid? selectedPlayerId;
    private int manualRoll = 1;
    private int minimumRoll = 1;
    private int maximumRoll = 999;
    private readonly IPartyList partyList;
    private readonly IObjectTable objectTable;
    private readonly IChatGui chatGui;

    public MainWindow(
        GameManager gameManager,
        IPartyList partyList,
        IObjectTable objectTable,
        IChatGui chatGui)
        : base("Hot Potato Game Manager")
    {
        this.gameManager = gameManager;
        this.partyList = partyList;
        this.objectTable = objectTable;
        this.chatGui = chatGui;

        this.chatGui.ChatMessage += OnChatMessage;
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

        /*
        * If the chat message includes a range, verify that it matches
        * the range selected when the game started.
        *
        * Example:
        * random! (1-300) 141
        *
        * Group 1 = 1
        * Group 2 = 300
        * Group 3 = 141
        */
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

            if (messageMinimum != gameManager.Settings.MinimumNumber
                || messageMaximum != gameManager.Settings.MaximumNumber)
            {
                statusMessage =
                    $"{senderName} used the wrong dice range. "
                    + $"Expected "
                    + $"{gameManager.Settings.MinimumNumber}-"
                    + $"{gameManager.Settings.MaximumNumber}, "
                    + $"but they used "
                    + $"{messageMinimum}-{messageMaximum}.";

                return;
            }
        }
        else if (gameManager.Settings.MinimumNumber != 1
                || gameManager.Settings.MaximumNumber != 999)
        {
            /*
            * A message without a displayed range is treated as the
            * standard 1-999 dice roll.
            */
            statusMessage =
                $"{senderName} used the standard dice range. "
                + $"Expected "
                + $"{gameManager.Settings.MinimumNumber}-"
                + $"{gameManager.Settings.MaximumNumber}.";

            return;
        }

        var player = gameManager.Players.FirstOrDefault(
            currentPlayer => string.Equals(
                currentPlayer.Name,
                senderName,
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
        if (!gameManager.ProcessRoll(
                player.Id,
                roll,
                out var isHotPotato))
        {
            statusMessage =
                $"Could not process {senderName}'s roll of {roll}.";

            return;
        }

        if (gameManager.IsGameComplete
            && gameManager.Winner is not null)
        {
            statusMessage = isHotPotato
                ? $"{player.Name} rolled {roll}. HOT POTATO! "
                + $"{gameManager.Winner.Name} wins!"
                : $"{player.Name} rolled {roll}. Safe.";

            return;
        }

        if (isHotPotato)
        {
            statusMessage =
                $"{player.Name} rolled {roll}. HOT POTATO! " +
                $"They were eliminated. Round {gameManager.CurrentRound} started. " +
                $"{gameManager.CurrentPlayer?.Name} rolls next.";
        }
        else
        {
            statusMessage =
                $"{player.Name} rolled {roll}. Safe. " +
                $"{gameManager.CurrentPlayer?.Name} rolls next.";
        }
    }
    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
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
            gameManager.AddPlayer(memberName);
            importedCount++;
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

    private void DrawStatus()
    {
        ImGui.Text("Game Status");

        if (gameManager.IsGameRunning)
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

        ImGui.Text("Roll Range");

        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Minimum##RollRange", ref minimumRoll);

        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Maximum##RollRange", ref maximumRoll);

        if (minimumRoll < 1)
        {
            minimumRoll = 1;
        }

        if (maximumRoll < minimumRoll)
        {
            maximumRoll = minimumRoll;
        }

       if (ImGui.Button("Start Game"))
        {
            if (minimumRoll < 1 || maximumRoll < minimumRoll)
            {
                statusMessage = "Enter a valid roll range.";
            }
            else
            {
                var rangeSize = maximumRoll - minimumRoll + 1;

                if (rangeSize < gameManager.Settings.InitialNumberCount)
                {
                    statusMessage =
                        $"The roll range must contain at least "
                        + $"{gameManager.Settings.InitialNumberCount} numbers.";
                }
                else
                {
                    gameManager.Settings.MinimumNumber = minimumRoll;
                    gameManager.Settings.MaximumNumber = maximumRoll;

                    if (gameManager.StartGame())
                    {
                        newestNumbers = gameManager.HotPotatoNumbers.ToList();

                        statusMessage =
                            $"Round 1 started with {newestNumbers.Count} Hot Potato numbers "
                            + $"using the range {minimumRoll}-{maximumRoll}.";
                    }
                    else
                    {
                        statusMessage =
                            "Add at least two players before starting the game.";
                    }
                }
            }
        }
    }

        private void DrawRunningGame()
        {
            ImGui.Text($"Round {gameManager.CurrentRound}");
            ImGui.Text($"Active Players: {gameManager.ActivePlayers.Count}");
            ImGui.Text(
                $"Total Hot Potato Numbers: {gameManager.HotPotatoNumbers.Count}");

            if (gameManager.CurrentPlayer is not null)
            {
                ImGui.Text(
                    $"Current Roller: {gameManager.CurrentPlayer.Name}");
            }
            if (gameManager.IsGameComplete && gameManager.Winner is not null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text($"WINNER: {gameManager.Winner.Name}");

                ImGui.Spacing();
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Hot Potato Numbers");
            ImGui.Spacing();

            DrawNumberGrid(gameManager.HotPotatoNumbers);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Players");
            ImGui.Spacing();

            foreach (var player in gameManager.Players)
            {
                var playerLabel = player.IsEliminated
                    ? $"{player.Name} - Eliminated"
                    : player.Name;

                var isSelected = selectedPlayerId == player.Id;

                if (ImGui.Selectable(
                    $"{playerLabel}##{player.Id}",
                    isSelected))
                {
                    selectedPlayerId = player.Id;
                }

                if (player.LastRoll.HasValue)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"Last roll: {player.LastRoll.Value}");
                }
            }
            if (!gameManager.IsGameComplete)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Process Roll");

                ImGui.SetNextItemWidth(120);
                ImGui.InputInt("##ManualRoll", ref manualRoll);

                ImGui.SameLine();

                if (ImGui.Button("Submit Roll"))
                {
                    if (!selectedPlayerId.HasValue)
                    {
                        statusMessage = "Select a player first.";
                    }
                    else if (gameManager.ProcessRoll(
                        selectedPlayerId.Value,
                        manualRoll,
                        out var isHotPotato))
                    {
                        var player = gameManager.Players.First(
                            currentPlayer =>
                                currentPlayer.Id == selectedPlayerId.Value);

                        if (gameManager.IsGameComplete
                            && gameManager.Winner is not null)
                        {
                            statusMessage = isHotPotato
                                ? $"{player.Name} rolled {manualRoll} and was eliminated. "
                                    + $"{gameManager.Winner.Name} wins!"
                                : $"{player.Name} rolled {manualRoll}. Safe.";
                        }
                        if (isHotPotato)
                        {
                            statusMessage =
                                gameManager.IsGameComplete
                                    ? $"{player.Name} rolled {manualRoll}. HOT POTATO! {gameManager.Winner?.Name} wins!"
                                    : $"{player.Name} rolled {manualRoll}. HOT POTATO! They were eliminated. Round {gameManager.CurrentRound} started. {gameManager.CurrentPlayer?.Name} rolls next.";
                        }
                        else
                        {
                            statusMessage =
                                $"{player.Name} rolled {manualRoll}. Safe. {gameManager.CurrentPlayer?.Name} rolls next.";
                        }
                    }
                    else
                    {
                        statusMessage = "That roll could not be processed.";
                    }
                }
            }
            ImGui.Spacing();

            if (!gameManager.IsGameComplete)
            {
                // if (ImGui.Button("Start Next Round"))
                // {
                //     newestNumbers = gameManager.StartNextRound();

                //     statusMessage =
                //         newestNumbers.Count > 0
                //             ? $"Round {gameManager.CurrentRound} started. Added: "
                //                 + string.Join(", ", newestNumbers)
                //             : "No additional numbers could be generated.";
                // }

                ImGui.SameLine();
            }

            ImGui.SameLine();

            if (ImGui.Button("Reset Game"))
            {
                    gameManager.ResetGame();

                    newestNumbers = Array.Empty<int>();
                    selectedPlayerId = null;
                    manualRoll = 1;
                    statusMessage = "The game was reset.";
            }
        }

    private static void DrawNumberGrid(
    IReadOnlyList<int> numbers)
{
    const int columns = 5;

    if (ImGui.BeginTable(
        "HotPotatoNumberTable",
        columns,
        ImGuiTableFlags.Borders
        | ImGuiTableFlags.SizingStretchSame))
    {
        foreach (var number in numbers)
        {
            ImGui.TableNextColumn();
            ImGui.Text(number.ToString());
        }

        ImGui.EndTable();
    }
}
    private void AddPlayer()
    {
        var nameBeingAdded = playerName.Trim();

        if (gameManager.AddPlayer(nameBeingAdded))
        {
            statusMessage = $"{nameBeingAdded} was added.";
            playerName = string.Empty;
        }
        else
        {
            statusMessage =
                "Enter a unique player name before adding a player.";
        }
    }
}