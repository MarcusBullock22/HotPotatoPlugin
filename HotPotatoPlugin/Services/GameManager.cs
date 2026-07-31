using System;
using System.Collections.Generic;
using System.Linq;
using HotPotatoPlugin.Models;

namespace HotPotatoPlugin.Services;

public sealed class GameManager
{
    private readonly List<Player> players = [];
    private readonly List<int> hotPotatoNumbers = [];
    private readonly Random random = new();

    public IReadOnlyList<Player> Players => players;

    public IReadOnlyList<int> HotPotatoNumbers => hotPotatoNumbers;

    public GameSettings Settings { get; } = new();

    public bool IsGameRunning { get; private set; }

    public int CurrentRound { get; private set; }

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

        if (!HasValidSettings())
        {
            return false;
        }

        IsGameRunning = true;
        CurrentRound = 1;

        hotPotatoNumbers.Clear();
        AddUniqueNumbers(Settings.InitialNumberCount);

        return true;
    }

    public IReadOnlyList<int> StartNextRound()
    {
        if (!IsGameRunning)
        {
            return Array.Empty<int>();
        }

        CurrentRound++;

        return AddUniqueNumbers(Settings.NumbersPerRound);
    }

    public void ResetGame()
    {
        IsGameRunning = false;
        CurrentRound = 0;

        hotPotatoNumbers.Clear();
        players.Clear();
    }

    private List<int> AddUniqueNumbers(int numberCount)
    {
        var availableNumbers = Enumerable
            .Range(
                Settings.MinimumNumber,
                Settings.MaximumNumber - Settings.MinimumNumber + 1)
            .Except(hotPotatoNumbers)
            .ToList();

        var actualCount = Math.Min(numberCount, availableNumbers.Count);
        var generatedNumbers = new List<int>();

        for (var index = 0; index < actualCount; index++)
        {
            var selectedIndex = random.Next(availableNumbers.Count);
            var selectedNumber = availableNumbers[selectedIndex];

            generatedNumbers.Add(selectedNumber);
            hotPotatoNumbers.Add(selectedNumber);

            availableNumbers.RemoveAt(selectedIndex);
        }

        hotPotatoNumbers.Sort();
        generatedNumbers.Sort();

        return generatedNumbers;
    }

    private bool HasValidSettings()
    {
        if (Settings.MinimumNumber >= Settings.MaximumNumber)
        {
            return false;
        }

        var availableNumberCount =
            Settings.MaximumNumber - Settings.MinimumNumber + 1;

        return Settings.InitialNumberCount > 0
            && Settings.InitialNumberCount <= availableNumberCount
            && Settings.NumbersPerRound > 0;
    }
}