using Godot;
using System;
using System.Collections.Generic;

public partial class ScoreboardUI : Control
{
    [Export] public NodePath NetworkManagerPath { get; set; }
    
    private NetworkManager _networkManager;
    private VBoxContainer _playersList;
    
    public override void _Ready()
    {
        // Initially hide the scoreboard
        Visible = false;
        
        _playersList = GetNode<VBoxContainer>("%PlayersList");
        _networkManager = GetNode<NetworkManager>(NetworkManagerPath);
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkManager not found at path: " + NetworkManagerPath);
            return;
        }
    }
    
    public override void _Process(double delta)
    {
        if (Visible)
        {
            UpdateScoreboard();
        }
    }
    
    private void UpdateScoreboard()
    {
        // Clear current entries
        foreach (var child in _playersList.GetChildren())
        {
            child.QueueFree();
        }
        
        // Get all players
        var players = _networkManager.GetPlayers();
        var sortedPlayers = new List<NetworkedPlayer>();
        
        // Collect and sort players by kills
        foreach (var player in players.Values)
        {
            if (player is NetworkedPlayer networkPlayer)
            {
                sortedPlayers.Add(networkPlayer);
            }
        }
        
        sortedPlayers.Sort((a, b) => b.Kills.CompareTo(a.Kills));
        
        // Add header
        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "Player", SizeFlagsHorizontal = Control.SizeFlags.Expand });
        header.AddChild(new Label { Text = "Kills", HorizontalAlignment = HorizontalAlignment.Center });
        header.AddChild(new Label { Text = "Deaths", HorizontalAlignment = HorizontalAlignment.Center });
        _playersList.AddChild(header);
        
        // Add each player
        foreach (var player in sortedPlayers)
        {
            var row = new HBoxContainer();
            
            var nameLabel = new Label
            {
                Text = $"Player {player.PlayerId}",
                SizeFlagsHorizontal = Control.SizeFlags.Expand
            };
            
            // Highlight local player
            if (player.IsMultiplayerAuthority())
            {
                nameLabel.Modulate = new Color(1.0f, 1.0f, 0.0f); // Yellow for local player
            }
            
            row.AddChild(nameLabel);
            row.AddChild(new Label { Text = player.Kills.ToString(), HorizontalAlignment = HorizontalAlignment.Center });
            row.AddChild(new Label { Text = player.Deaths.ToString(), HorizontalAlignment = HorizontalAlignment.Center });
            
            _playersList.AddChild(row);
        }
    }
}