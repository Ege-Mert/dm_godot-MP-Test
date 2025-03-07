using Godot;
using System;

public partial class GameHUD : Control
{
    private Label _healthLabel;
    private Label _ammoLabel;
    private Label _killsLabel;
    private ProgressBar _healthBar;
    
    private NetworkedPlayer _localPlayer;
    
    public override void _Ready()
    {
        _healthLabel = GetNode<Label>("%HealthLabel");
        _healthBar = GetNode<ProgressBar>("%HealthBar");
        _ammoLabel = GetNode<Label>("%AmmoLabel");
        _killsLabel = GetNode<Label>("%KillsLabel");
        
        // Find local player (may not be available immediately)
        CallDeferred(nameof(FindLocalPlayer));
    }
    
    public override void _Process(double delta)
    {
        if (_localPlayer != null && IsInstanceValid(_localPlayer))
        {
            UpdateHUD();
        }
        else
        {
            FindLocalPlayer();
        }
    }
    
    private void FindLocalPlayer()
    {
        // Find the player node that belongs to the local client
        var players = GetTree().GetNodesInGroup("Players");
        
        foreach (var player in players)
        {
            if (player is NetworkedPlayer networkPlayer && networkPlayer.IsMultiplayerAuthority())
            {
                _localPlayer = networkPlayer;
                break;
            }
        }
    }
    
    private void UpdateHUD()
    {
        // Update health display
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"Health: {_localPlayer.Health}";
        }
        
        if (_healthBar != null)
        {
            _healthBar.Value = _localPlayer.Health;
            
            // Change color based on health
            if (_localPlayer.Health < 25)
            {
                _healthBar.Modulate = new Color(1.0f, 0.0f, 0.0f); // Red for critical health
            }
            else if (_localPlayer.Health < 50)
            {
                _healthBar.Modulate = new Color(1.0f, 0.5f, 0.0f); // Orange for low health
            }
            else
            {
                _healthBar.Modulate = new Color(0.0f, 1.0f, 0.0f); // Green for good health
            }
        }
        
        // Update kills display
        if (_killsLabel != null)
        {
            _killsLabel.Text = $"Kills: {_localPlayer.Kills}";
        }
        
        // Update ammo display (for future use)
        if (_ammoLabel != null)
        {
            _ammoLabel.Text = "Ammo: ∞"; // Infinite ammo for now
        }
    }
}