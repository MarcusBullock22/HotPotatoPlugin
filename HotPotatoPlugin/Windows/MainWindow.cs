using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HotPotatoPlugin.Services;
using System;
using System.Collections.Generic;

namespace HotPotatoPlugin.Windows;

public sealed class MainWindow : Window
{
    private readonly GameManager gameManager;
    private IReadOnlyList<int> newestNumbers = Array.Empty<int>();
    private string playerName = string.Empty;
    private string statusMessage = string.Empty;
    private Guid? selectedPlayerId;
    private int manualRoll = 1;
    public MainWindow(GameManager gameManager)
        : base("Hot Potato Game Manager")
    {
        this.gameManager = gameManager;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 350),
            MaximumSize = new Vector2(900, 900)
        };
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

        if (ImGui.Button("Start Game"))
        {
            if (gameManager.StartGame())
            {
                newestNumbers = gameManager.HotPotatoNumbers.ToList();

                statusMessage =
                    $"Round 1 started with {newestNumbers.Count} Hot Potato numbers.";
            }
            else
            {
                statusMessage =
                    "Add at least two players before starting the game.";
            }
        }
    }

        private void DrawRunningGame()
        {
            ImGui.Text($"Round {gameManager.CurrentRound}");
            ImGui.Text($"Active Players: {gameManager.ActivePlayers.Count}");
            ImGui.Text(
                $"Total Hot Potato Numbers: {gameManager.HotPotatoNumbers.Count}");

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
                        else
                        {
                            statusMessage = isHotPotato
                                ? $"{player.Name} rolled {manualRoll}. "
                                    + "HOT POTATO! They were eliminated."
                                : $"{player.Name} rolled {manualRoll}. Safe.";
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
                if (ImGui.Button("Start Next Round"))
                {
                    newestNumbers = gameManager.StartNextRound();

                    statusMessage =
                        newestNumbers.Count > 0
                            ? $"Round {gameManager.CurrentRound} started. Added: "
                                + string.Join(", ", newestNumbers)
                            : "No additional numbers could be generated.";
                }

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