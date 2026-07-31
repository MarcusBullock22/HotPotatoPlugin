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

    public Guid? CurrentPlayerId { get; private set; }

    public Player? CurrentPlayer =>
        CurrentPlayerId is null
            ? null
            : players.FirstOrDefault(
                player => player.Id == CurrentPlayerId);

    public IReadOnlyList<Player> ActivePlayers =>
        players
            .Where(player => !player.IsEliminated)
            .ToList();

    public bool IsGameComplete =>
        IsGameRunning
        && players.Count > 1
        && players.Count(player => !player.IsEliminated) == 1;

    public Player? Winner =>
        IsGameComplete
            ? players.Single(player => !player.IsEliminated)
            : null;

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

        foreach (var player in players)
        {
            player.IsEliminated = false;
            player.LastRoll = null;
        }

        IsGameRunning = true;
        CurrentRound = 1;

        hotPotatoNumbers.Clear();
        AddUniqueNumbers(Settings.InitialNumberCount);

        CurrentPlayerId = players
            .FirstOrDefault(player => !player.IsEliminated)
            ?.Id;

        return true;
    }

    public bool ProcessRoll(
        Guid playerId,
        int roll,
        out bool isHotPotato)
    {
        isHotPotato = false;

        if (!IsGameRunning || IsGameComplete)
        {
            return false;
        }

        if (roll < Settings.MinimumNumber
            || roll > Settings.MaximumNumber)
        {
            return false;
        }

        var player = players.FirstOrDefault(
            currentPlayer => currentPlayer.Id == playerId);

        if (player is null || player.IsEliminated)
        {
            return false;
        }

        if (CurrentPlayerId != playerId)
        {
            return false;
        }

        player.LastRoll = roll;

        isHotPotato = hotPotatoNumbers.Contains(roll);

        if (!isHotPotato)
        {
            AdvanceToNextActivePlayer(player.Id);
            return true;
        }

        player.IsEliminated = true;

        var remainingPlayerCount = players.Count(
            currentPlayer => !currentPlayer.IsEliminated);

        if (remainingPlayerCount == 1)
        {
            CurrentPlayerId = null;
            return true;
        }

        CurrentRound++;

        AddUniqueNumbers(Settings.NumbersPerRound);

        AdvanceToNextActivePlayer(player.Id);

        return true;
    }

    public void ResetGame()
    {
        hotPotatoNumbers.Clear();

        foreach (var player in players)
        {
            player.IsEliminated = false;
            player.LastRoll = null;
        }

        CurrentRound = 0;
        IsGameRunning = false;
        CurrentPlayerId = null;
    }

    private void AdvanceToNextActivePlayer(Guid currentPlayerId)
    {
        if (players.Count == 0)
        {
            CurrentPlayerId = null;
            return;
        }

        var currentIndex = players.FindIndex(
            player => player.Id == currentPlayerId);

        if (currentIndex < 0)
        {
            CurrentPlayerId = players
                .FirstOrDefault(player => !player.IsEliminated)
                ?.Id;

            return;
        }

        for (var offset = 1; offset <= players.Count; offset++)
        {
            var nextIndex =
                (currentIndex + offset) % players.Count;

            var nextPlayer = players[nextIndex];

            if (!nextPlayer.IsEliminated)
            {
                CurrentPlayerId = nextPlayer.Id;
                return;
            }
        }

        CurrentPlayerId = null;
    }

    private List<int> AddUniqueNumbers(int numberCount)
    {
        var availableNumbers = Enumerable
            .Range(
                Settings.MinimumNumber,
                Settings.MaximumNumber
                - Settings.MinimumNumber
                + 1)
            .Except(hotPotatoNumbers)
            .ToList();

        var actualCount = Math.Min(
            numberCount,
            availableNumbers.Count);

        var generatedNumbers = new List<int>();

        for (var index = 0; index < actualCount; index++)
        {
            var selectedIndex = random.Next(
                availableNumbers.Count);

            var selectedNumber =
                availableNumbers[selectedIndex];

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
            Settings.MaximumNumber
            - Settings.MinimumNumber
            + 1;

        return Settings.InitialNumberCount > 0
            && Settings.InitialNumberCount <= availableNumberCount
            && Settings.NumbersPerRound > 0;
    }
}