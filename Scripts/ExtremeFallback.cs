using Godot;
using System;

// EXTREME FALLBACK - this is only added as a standalone script
// for when all other camera solutions fail
public partial class ExtremeFallback : Control
{
    private bool _hasShownFallback = false;
    private double _timeSinceStart = 0;
    private Label _statusLabel;
    
    public override void _Ready()
    {
        // Create a 2D UI that will always be visible
        _statusLabel = new Label();
        _statusLabel.Text = "Loading game scene...";
        _statusLabel.Position = new Vector2(20, 20);
        _statusLabel.ZIndex = 1000; // Ensure it's on top
        
        AddChild(_statusLabel);
    }
    
    public override void _Process(double delta)
    {
        _timeSinceStart += delta;
        
        // Only show fallback after 3 seconds if camera initialization has failed
        if (_timeSinceStart > 3.0 && !_hasShownFallback)
        {
            // Update status
            _statusLabel.Text = "FALLBACK CAMERA SYSTEM ACTIVATED";
            
            // Create a fallback 2D representation
            CreateFallbackUI();
            
            _hasShownFallback = true;
        }
        
        // Update player positions when using fallback
        if (_hasShownFallback)
        {
            UpdatePlayerPositions();
        }
    }
    
    private void CreateFallbackUI()
    {
        // Create a ColorRect as the ground
        var ground = new ColorRect();
        ground.Color = new Color(0.2f, 0.7f, 0.2f);
        ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ground.Position = new Vector2(0, 400);
        ground.Size = new Vector2(1024, 200);
        ground.ZIndex = -1;
        
        AddChild(ground);
        
        // Create a sky
        var sky = new ColorRect();
        sky.Color = new Color(0.4f, 0.6f, 1.0f);
        sky.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        sky.Position = new Vector2(0, 0);
        sky.Size = new Vector2(1024, 400);
        sky.ZIndex = -2;
        
        AddChild(sky);
        
        // Create a sun
        var sun = new ColorRect();
        sun.Color = new Color(1.0f, 1.0f, 0.0f);
        sun.Position = new Vector2(100, 50);
        sun.Size = new Vector2(50, 50);
        sun.ZIndex = -1;
        
        AddChild(sun);
        
        // Instructions
        var instructions = new Label();
        instructions.Text = "EMERGENCY FALLBACK VIEW\n" +
                          "If you see this, camera initialization failed.\n" +
                          "This is a simplified 2D representation of the game.\n\n" +
                          "Players:";
        instructions.Position = new Vector2(400, 50);
        instructions.ZIndex = 10;
        
        AddChild(instructions);
    }
    
    private void UpdatePlayerPositions()
    {
        // Find all players
        var players = GetTree().GetNodesInGroup("Players");
        
        // Clear previous representations
        foreach (var child in GetChildren())
        {
            if (child is ColorRect rect && rect.Name.ToString().StartsWith("Player_"))
            {
                rect.QueueFree();
            }
            else if (child is Label label && label.Name.ToString().StartsWith("Label_"))
            {
                label.QueueFree();
            }
        }
        
        // Create a representation for each player
        foreach (var player in players)
        {  
            if (player is Node3D node3D)
            {
                // Calculate 2D position from 3D
                var pos3D = node3D.GlobalPosition;
                var pos2D = new Vector2(pos3D.X * 10 + 512, 400 - pos3D.Z * 10);
                
                string playerName = node3D.Name.ToString();
                string uniqueId = Multiplayer.GetUniqueId().ToString();
                bool isLocalPlayer = playerName == uniqueId;
                
                // Create a colored rectangle for the player
                var rect = new ColorRect();
                rect.Name = $"Player_{playerName}";
                rect.Color = isLocalPlayer ? 
                             new Color(0, 0, 1) : // Blue for local
                             new Color(1, 0, 0);  // Red for others
                rect.Position = pos2D;
                rect.Size = new Vector2(20, 40);
                rect.ZIndex = 5;
                
                AddChild(rect);
                
                // Add label with ID
                var label = new Label();
                label.Name = $"Label_{playerName}";
                label.Text = $"Player {playerName}";
                label.Position = new Vector2(pos2D.X - 20, pos2D.Y - 20);
                label.ZIndex = 6;
                
                if (isLocalPlayer)
                {
                    label.Text += " (YOU)";
                    label.Modulate = new Color(0, 1, 1); // Cyan for local player
                }
                
                AddChild(label);
                
                // Add player controls info if local player
                if (isLocalPlayer)
                {
                    var controls = new Label();
                    controls.Text = "Controls:\n" +
                                   "WASD: Move\n" +
                                   "Space: Jump\n" +
                                   "Left Click: Fire\n" +
                                   "Shift: Sprint";
                    controls.Position = new Vector2(20, 220);
                    controls.ZIndex = 10;
                    controls.Modulate = new Color(1, 1, 0); // Yellow
                    
                    AddChild(controls);
                }
            }
        }
        
        // Update status text with player count
        _statusLabel.Text = $"FALLBACK VIEW: {players.Count} players connected";
    }
}
