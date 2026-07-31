using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HotPotatoPlugin.Services;

namespace HotPotatoPlugin.Windows;

public sealed class MainWindow : Window
{
    private readonly GameManager gameManager;

    private string playerName = string.Empty;
    private string statusMessage = string.Empty;

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
                statusMessage = "The game has started.";
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
        ImGui.Text("Active Players");
        ImGui.Spacing();

        foreach (var player in gameManager.Players)
        {
            ImGui.BulletText(player.Name);
        }

        ImGui.Spacing();

        if (ImGui.Button("Reset Game"))
        {
            gameManager.ResetGame();
            statusMessage = "The game was reset.";
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