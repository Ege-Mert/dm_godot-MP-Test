using Godot;
using System;

/// <summary>
/// Helper tool to set up proper MultiplayerSynchronizer configuration.
/// Include this in your NetworkedPlayer scene to properly set up replication.
/// </summary>
public partial class UpdateMultiplayerSynchronizer : Node
{
    [Export] public NodePath SynchronizerPath = "MultiplayerSynchronizer";
    
    public override void _Ready()
    {
        var synchronizer = GetNode<MultiplayerSynchronizer>(SynchronizerPath);
        if (synchronizer == null)
        {
            GD.PrintErr("MultiplayerSynchronizer not found at path: " + SynchronizerPath);
            return;
        }
        
        // Set up the synchronizer with proper properties
        var config = new SceneReplicationConfig();
        
        // Add properties to sync
        string[] syncProperties = new string[]
        {
            "position",   // Node3D position
            "rotation",   // Node3D rotation
            "velocity",   // CharacterBody3D velocity
            "health",     // NetworkedPlayer health property
            "kills",      // NetworkedPlayer kills property
            "deaths"      // NetworkedPlayer deaths property
        };
        
        foreach (var property in syncProperties)
        {
            // Add each property with NODE_PATH:property_name format
            config.AddProperty(".:"+property);
        }
        
        // Set replication interval (how often to sync)
        synchronizer.ReplicationInterval = 0.05f; // 20 times per second
        
        // Apply the configuration
        synchronizer.ReplicationConfig = config;
        
        GD.Print("MultiplayerSynchronizer configured successfully");
    }
}