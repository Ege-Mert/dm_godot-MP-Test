using Godot;
using System;

// This is a more direct approach to fixing camera issues
// Add this to the NetworkedPlayer scene
public partial class CameraFixer : Node
{
    [Export] public bool IsAlwaysActive { get; set; } = false;
    
    private Camera3D _camera;
    private bool _cameraFound = false;
    private double _checkTimer = 0;
    
    public override void _Ready()
    {
        // Find a camera in our parent or siblings
        FindAndCacheCamera();
    }
    
    public override void _Process(double delta)
    {
        _checkTimer += delta;
        
        // Keep checking for cameras periodically
        if (!_cameraFound && _checkTimer > 0.5)
        {
            FindAndCacheCamera();
            _checkTimer = 0;
        }
        
        if (_camera != null)
        {
            // Check if this is a child of a player node
            var player = GetParentPlayer();
            if (player != null)
            {
                var playerName = player.Name.ToString();
                var uniqueId = Multiplayer.GetUniqueId().ToString();
                
                if (playerName == uniqueId || IsAlwaysActive)
                {
                    // If this is the local player's camera, force activate it
                    if (!_camera.Current)
                    {
                        GD.Print($"CameraFixer: Forcing camera activation for player {playerName}");
                        _camera.Current = true;
                        
                        // Add visual indicator
                        AddVisualIndicator();
                    }
                }
            }
        }
    }
    
    private void FindAndCacheCamera()
    {
        // Look for camera in parent node
        Node parent = GetParent();
        if (parent != null)
        {
            // Try finding in direct child
            _camera = FindCameraInChildren(parent);
            
            if (_camera != null)
            {
                _cameraFound = true;
                GD.Print($"CameraFixer: Found camera at {_camera.GetPath()}");
            }
        }
    }
    
    private Camera3D FindCameraInChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Camera3D camera)
            {
                return camera;
            }
            
            // Also search in child's children (recursive)
            var result = FindCameraInChildren(child);
            if (result != null)
                return result;
        }
        
        return null;
    }
    
    private NetworkedPlayer GetParentPlayer()
    {
        Node current = this;
        while (current != null)
        {
            if (current is NetworkedPlayer player)
                return player;
                
            current = current.GetParent();
        }
        
        return null;
    }
    
    private void AddVisualIndicator()
    {
        if (_camera.HasNode("FixerIndicator"))
            return;
            
        // Add a visual in front of the camera
        var box = new CsgBox3D();
        box.Name = "FixerIndicator";
        box.Size = new Vector3(0.1f, 0.1f, 0.1f);
        box.Position = new Vector3(0, 0, -0.5f);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1, 0, 1); // Magenta
        material.Emission = new Color(1, 0, 1);
        material.EmissionEnergyMultiplier = 1.0f;
        box.MaterialOverride = material;
        
        _camera.AddChild(box);
        
        // Add a label
        var label = new Label3D();
        label.Text = "CAMERA FIXED!";
        label.Position = new Vector3(0, 0.1f, -0.5f);
        label.FontSize = 12;
        label.Scale = new Vector3(0.1f, 0.1f, 0.1f);
        
        _camera.AddChild(label);
    }
}
