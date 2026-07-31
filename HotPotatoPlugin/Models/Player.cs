using System;

namespace HotPotatoPlugin.Models;

public sealed class Player
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; }

    public Player(string name)
    {
        Name = name;
    }
}