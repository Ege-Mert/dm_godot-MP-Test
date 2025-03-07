using Godot;
using System;

// This script forces camera activation for clients
public partial class ForceCamera : Node
{
    private bool _cameraFixed = false;
    private double _timeSinceStart = 0;
    
    public override void _Ready()
    {
        // Makes sure this node runs with high priority
        ProcessPriority = -1000;
    }
    
    public override void _Process(double delta)
    {
        _timeSinceStart += delta;
        
        // Try to fix cameras every frame for the first 5 seconds
        if (_timeSinceStart < 5.0 || !_cameraFixed)
        {
            FindAndFixCameras();
        }
        
        // Create emergency default camera if needed
        if (_timeSinceStart > 1.0 && !_cameraFixed)
        {
            CreateEmergencyCamera();
            _cameraFixed = true;
        }
    }
    
    private void FindAndFixCameras()
    {
        // Find all cameras in the scene
        var cameras = GetTree().GetNodesInGroup("Cameras");
        
        // Also look for cameras that aren't in the group
        var allCameras = new Godot.Collections.Array<Camera3D>();
        FindCamerasRecursive(GetTree().Root, allCameras);
        
        GD.Print($"Found {cameras.Count} cameras in 'Cameras' group + {allCameras.Count} total cameras");
        
        // Try to force-activate a camera
        bool foundActiveCamera = false;
        
        // First try networked player cameras
        foreach (var camera in allCameras)
        {
            if (camera.GetParent() != null && camera.GetParent().Name.ToString() == "Head")
            {
                var player = camera.GetParent().GetParent();
                if (player != null && player is NetworkedPlayer)
                {
                    GD.Print($"Found player camera: {camera.GetPath()}");
                    
                    // Try to activate this camera
                    camera.Current = true;
                    foundActiveCamera = true;
                    
                    // Add a visual indicator
                    AddCameraIndicator(camera);
                    
                    break;
                }
            }
        }
        
        // If no player camera was found/activated, try any camera
        if (!foundActiveCamera && allCameras.Count > 0)
        {
            GD.Print($"Activating first available camera: {allCameras[0].GetPath()}");
            allCameras[0].Current = true;
            AddCameraIndicator(allCameras[0]);
            foundActiveCamera = true;
        }
        
        _cameraFixed = foundActiveCamera;
    }
    
    private void FindCamerasRecursive(Node node, Godot.Collections.Array<Camera3D> cameras)
    {
        if (node is Camera3D camera)
        {
            cameras.Add(camera);
        }
        
        foreach (var child in node.GetChildren())
        {
            FindCamerasRecursive(child, cameras);
        }
    }
    
    private void AddCameraIndicator(Camera3D camera)
    {
        // Don't add indicators multiple times
        if (camera.HasNode("DebugIndicator"))
            return;
            
        GD.Print($"Adding debug indicator to camera: {camera.GetPath()}");
        
        // Add a visual in front of the camera
        var sphere = new CsgSphere3D();
        sphere.Name = "DebugIndicator";
        sphere.Radius = 0.2f;
        sphere.Position = new Vector3(0, 0, -1);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0, 1, 0); // Bright green
        material.Emission = new Color(0, 1, 0);
        material.EmissionEnergyMultiplier = 1.0f;
        sphere.MaterialOverride = material;
        
        camera.AddChild(sphere);
    }
    
    private void CreateEmergencyCamera()
    {
        GD.Print("Creating emergency camera!");
        
        // Create a new camera as last resort
        var camera = new Camera3D();
        camera.Name = "EmergencyCamera";
        camera.Position = new Vector3(0, 10, 0);
        camera.RotationDegrees = new Vector3(-90, 0, 0); // Look down
        camera.Current = true;
        
        // Add visual indicator
        var sphere = new CsgSphere3D();
        sphere.Name = "EmergencyIndicator";
        sphere.Radius = 0.5f;
        sphere.Position = new Vector3(0, 0, -2);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1, 0, 0); // Bright red
        material.Emission = new Color(1, 0, 0);
        material.EmissionEnergyMultiplier = 1.0f;
        sphere.MaterialOverride = material;
        
        camera.AddChild(sphere);
        
        // Add to scene
        GetTree().CurrentScene.AddChild(camera);
        
        // Create giant indicators
        CreateGiantIndicators();
    }
    
    private void CreateGiantIndicators()
    {
        // Create a giant platform so we're sure something is visible
        var platform = new CsgBox3D();
        platform.Name = "EmergencyPlatform";
        platform.Size = new Vector3(100, 1, 100);
        platform.Position = new Vector3(0, -5, 0);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.2f, 0.8f, 0.2f); // Green
        platform.MaterialOverride = material;
        
        GetTree().CurrentScene.AddChild(platform);
        
        // Create a giant colored pillar
        var pillar = new CsgCylinder3D();
        pillar.Name = "EmergencyPillar";
        pillar.Radius = 3.0f;
        pillar.Height = 30.0f;
        pillar.Position = new Vector3(0, 10, 0);
        
        var pillarMaterial = new StandardMaterial3D();
        pillarMaterial.AlbedoColor = new Color(1, 0, 1); // Magenta
        pillarMaterial.Emission = new Color(1, 0, 1);
        pillarMaterial.EmissionEnergyMultiplier = 0.5f;
        pillar.MaterialOverride = pillarMaterial;
        
        GetTree().CurrentScene.AddChild(pillar);
        
        // Create a giant floating text
        var label = new Label3D();
        label.Name = "EmergencyLabel";
        label.Text = "EMERGENCY CAMERA ACTIVATED!\nIf you see this, camera initialization failed.\nPlease check console logs.";
        label.Position = new Vector3(0, 15, 0);
        label.FontSize = 100;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = true;
        label.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        label.Scale = new Vector3(0.1f, 0.1f, 0.1f);
        
        GetTree().CurrentScene.AddChild(label);
    }
}