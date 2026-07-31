using System;

namespace HotPotatoPlugin.Models;

public sealed class Player
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; }

    public int? LastRoll { get; set; }

    public bool IsEliminated { get; set; }

    public Player(string name)
    {
        Name = name;
    }

    public bool IsRemoved { get; set; }

    public bool IsActive =>
    !IsEliminated && !IsRemoved;
}