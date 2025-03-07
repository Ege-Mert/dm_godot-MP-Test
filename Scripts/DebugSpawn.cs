using Godot;
using System;

public partial class DebugSpawn : Node3D
{
    public override void _Ready()
    {
        // Add this node to the SpawnPoints group
        AddToGroup("SpawnPoints");
        GD.Print($"Debug spawn point registered at {GlobalPosition}");
    }
}