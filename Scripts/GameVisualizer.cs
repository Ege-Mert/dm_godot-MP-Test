using Godot;
using System;

public partial class GameVisualizer : Node
{
    public override void _Ready()
    {
        // Create visual elements to help debug the scene
        CreateDebugVisuals();
    }
    
    private void CreateDebugVisuals()
    {
        // Create a prominent sphere in the center
        var sphere = new CsgSphere3D();
        sphere.Name = "CenterSphere";
        sphere.Radius = 2.0f;
        sphere.Position = new Vector3(0, 4, 0);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1.0f, 0.2f, 0.8f); // Bright magenta/pink
        material.Emission = new Color(1.0f, 0.2f, 0.8f);
        material.EmissionEnergyMultiplier = 0.5f;
        sphere.MaterialOverride = material;
        
        AddChild(sphere);
        
        // Add a floating label
        var label = new Label3D();
        label.Text = "GAME SCENE LOADED\nLook for this object!";
        label.Position = new Vector3(0, 6, 0);
        label.FontSize = 64;
        // label.BillboardMode = BaseMaterial3D.BillboardModeEnum.YBillboard;
        label.NoDepthTest = true;
        
        AddChild(label);
        
        // Add a spotlight to highlight the area
        var light = new SpotLight3D();
        light.Position = new Vector3(0, 10, 0);
        light.LightColor = new Color(1.0f, 1.0f, 0.5f);
        light.LightEnergy = 5.0f;
        light.SpotRange = 15.0f;
        light.SpotAngle = 30.0f;
        light.RotationDegrees = new Vector3(-90, 0, 0);
        
        AddChild(light);
        
        // Rotate the sphere to make it more obvious
        var animationPlayer = new AnimationPlayer();
        AddChild(animationPlayer);
        
        var animation = new Animation();
        animation.Length = 4.0f;
        animation.LoopMode = Animation.LoopModeEnum.Linear;
        
        // Add rotation track
        var trackIdx = animation.AddTrack(Animation.TrackType.Rotation3D);
        animation.TrackSetPath(trackIdx, "CenterSphere:rotation");
        
        // Start rotation
        animation.RotationTrackInsertKey(trackIdx, 0.0f, new Quaternion(Vector3.Up, 0));
        // End rotation
        animation.RotationTrackInsertKey(trackIdx, 4.0f, new Quaternion(Vector3.Up, Mathf.Tau));
        
        // Add animation to library
        var animLib = new AnimationLibrary();
        animLib.AddAnimation("rotate", animation);
        animationPlayer.AddAnimationLibrary("default", animLib);
        
        // Play the animation
        animationPlayer.Play("default/rotate");
        
        GD.Print("Debug visuals created in game scene");
    }
}