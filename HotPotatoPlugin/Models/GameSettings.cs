namespace HotPotatoPlugin.Models;

public sealed class GameSettings
{
    public int MinimumNumber { get; set; } = 1;

    public int MaximumNumber { get; set; } = 999;

    public int InitialNumberCount { get; set; } = 25;

    public int NumbersPerRound { get; set; } = 5;
}