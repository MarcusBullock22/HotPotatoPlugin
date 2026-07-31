namespace HotPotatoPlugin.Models;

public sealed class GameSettings
{
    public int MinimumNumber => 1;

    public int MaximumNumber { get; set; } = 999;

    public int InitialNumberCount { get; set; } = 25;

    public int NumbersPerRound { get; set; } = 5;

    public long EntryFee { get; set; } = 100_000;

    public float HouseCutPercent { get; set; } = 30f;
}