using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkDiagnostics : Node
{
    [Export] public Label DiagnosticsLabel { get; set; }
    [Export] public float UpdateIntervalSeconds { get; set; } = 1.0f;
    private float _timeSinceLastUpdate = 0;
    
    private NetworkManager _networkManager;
    
    public override void _Ready()
    {
        GD.Print("NetworkDiagnostics: Initialized");
        
        // Find NetworkManager
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkDiagnostics: NetworkManager not found!");
        }
        
        // Create a label if not provided
        if (DiagnosticsLabel == null)
        {
            var mainScene = GetTree().CurrentScene;
            if (mainScene != null)
            {
                try
                {
                    DiagnosticsLabel = mainScene.GetNode<Label>("CanvasLayer/DebugInfo/Label");
                    if (DiagnosticsLabel != null)
                    {
                        GD.Print("NetworkDiagnostics: Found debug label in scene");
                    }
                }
                catch
                {
                    GD.PrintErr("NetworkDiagnostics: Could not find debug label in scene");
                }
            }
        }
        
        UpdateDiagnostics();
    }
    
    public override void _Process(double delta)
    {
        _timeSinceLastUpdate += (float)delta;
        
        if (_timeSinceLastUpdate >= UpdateIntervalSeconds)
        {
            UpdateDiagnostics();
            _timeSinceLastUpdate = 0;
        }
    }
    
    private void UpdateDiagnostics()
    {
        if (DiagnosticsLabel == null) return;
        
        try
        {
            string info = "Network Diagnostics\n";
            info += $"My ID: {Multiplayer.GetUniqueId()}\n";
            info += $"Is Server: {(_networkManager?.IsHost() == true ? "Yes" : "No")}\n";
            info += $"Peers: {string.Join(", ", Multiplayer.GetPeers())}\n";
            
            // Check camera and display info
            var player = FindLocalPlayer();
            if (player != null)
            {
                info += "Player found in scene!\n";
                
                var camera = FindCameraInPlayer(player);
                if (camera != null)
                {
                    info += $"Camera found! Current: {camera.Current}\n";
                    info += $"Position: {player.GlobalPosition}\n";
                }
                else
                {
                    info += "No camera found in player!\n";
                }
            }
            else
            {
                info += "Local player not found in scene!\n";
                
                // List all players in scene
                var allPlayers = GetTree().GetNodesInGroup("Players");
                info += $"Total players in scene: {allPlayers.Count}\n";
                foreach (var p in allPlayers)
                {
                    if (p is CharacterBody3D body)
                    {
                        info += $"Player at {body.GlobalPosition}\n";
                    }
                }
            }
            
            DiagnosticsLabel.Text = info;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error updating diagnostics: {e.Message}");
        }
    }
    
    private Node3D FindLocalPlayer()
    {
        var playerId = Multiplayer.GetUniqueId();
        var allPlayers = GetTree().GetNodesInGroup("Players");
        
        foreach (var player in allPlayers)
        {
            if (player is Node3D playerNode && 
                player.GetMultiplayerAuthority() == playerId)
            {
                return playerNode;
            }
        }
        
        return null;
    }
    
    private Camera3D FindCameraInPlayer(Node3D player)
    {
        // Try direct child first
        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera != null) return camera;
        
        // Search for any camera in children
        var cameras = new List<Camera3D>();
        FindCamerasRecursive(player, cameras);
        
        return cameras.Count > 0 ? cameras[0] : null;
    }
    
    private void FindCamerasRecursive(Node node, List<Camera3D> cameras)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Camera3D camera)
            {
                cameras.Add(camera);
            }
            
            FindCamerasRecursive(child, cameras);
        }
    }
}