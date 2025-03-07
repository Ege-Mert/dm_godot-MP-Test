using Godot;
using System;

// This script automatically adds itself to any scene it's run in
// Best used as an autoload/singleton
public partial class AutoVisualizer : Node
{
    private bool _visualsCreated = false;
    
    public override void _Ready()
    {
        // Makes sure this node runs at the beginning when added to a scene
        ProcessPriority = -100;
    }
    
    public override void _Process(double delta)
    {
        // Only create the visuals once
        if (!_visualsCreated && GetTree().CurrentScene != null)
        {
            if (GetTree().CurrentScene.GetPath().ToString().Contains("GameScene.tscn"))
            {
                CreateDebugVisuals();
                _visualsCreated = true;
                
                // Stop processing after creating visuals
                SetProcess(false);
            }
        }
    }
    
    private void CreateDebugVisuals()
    {
        GD.Print("Creating debug visuals in: " + GetTree().CurrentScene.Name);
        
        // Create a prominent sphere in the center
        var sphere = new CsgSphere3D();
        sphere.Name = "DebugSphere";
        sphere.Radius = 2.0f;
        sphere.Position = new Vector3(0, 4, 0);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1.0f, 0.2f, 0.8f); // Bright magenta/pink
        material.Emission = new Color(1.0f, 0.2f, 0.8f);
        material.EmissionEnergyMultiplier = 0.5f;
        sphere.MaterialOverride = material;
        
        GetTree().CurrentScene.AddChild(sphere);
        
        // Add a floating label
        var label = new Label3D();
        label.Text = "GAME SCENE LOADED\nLook for this object!";
        label.Position = new Vector3(0, 6, 0);
        label.FontSize = 64;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        label.NoDepthTest = true;
        
        GetTree().CurrentScene.AddChild(label);
        
        // Add a spotlight to highlight the area
        var light = new SpotLight3D();
        light.Position = new Vector3(0, 10, 0);
        light.LightColor = new Color(1.0f, 1.0f, 0.5f);
        light.LightEnergy = 5.0f;
        light.SpotRange = 15.0f;
        light.SpotAngle = 30.0f;
        light.RotationDegrees = new Vector3(-90, 0, 0);
        
        GetTree().CurrentScene.AddChild(light);
        
        GD.Print("Debug visuals created in game scene");
    }
}