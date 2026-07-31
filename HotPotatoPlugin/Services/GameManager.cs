using System;
using System.Collections.Generic;
using System.Linq;
using HotPotatoPlugin.Models;

namespace HotPotatoPlugin.Services;

public sealed class GameManager
{
    private readonly List<Player> players = [];

    public IReadOnlyList<Player> Players => players;

    public bool IsGameRunning { get; private set; }

    public bool AddPlayer(string playerName)
    {
        var cleanedName = playerName.Trim();

        if (string.IsNullOrWhiteSpace(cleanedName))
        {
            return false;
        }

        var alreadyExists = players.Any(
            player => player.Name.Equals(
                cleanedName,
                StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return false;
        }

        players.Add(new Player(cleanedName));

        return true;
    }

    public bool RemovePlayer(Guid playerId)
    {
        if (IsGameRunning)
        {
            return false;
        }

        var player = players.FirstOrDefault(
            currentPlayer => currentPlayer.Id == playerId);

        if (player is null)
        {
            return false;
        }

        players.Remove(player);

        return true;
    }

    public bool StartGame()
    {
        if (players.Count < 2)
        {
            return false;
        }

        IsGameRunning = true;

        return true;
    }

    public void ResetGame()
    {
        IsGameRunning = false;
        players.Clear();
    }
}