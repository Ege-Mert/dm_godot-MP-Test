using Godot;
using System;

public partial class Weapon : Node3D
{
    [Export] public int Damage { get; set; } = 10;
    [Export] public float FireRate { get; set; } = 0.5f; // Seconds between shots
    [Export] public float Range { get; set; } = 100.0f;
    [Export] public PackedScene BulletImpactEffect { get; set; }
    [Export] public string FireActionName { get; set; } = "fire";
    
    private Camera3D _camera;
    private RayCast3D _rayCast;
    private double _timeSinceLastShot = 0;
    private bool _canFire = true;
    private bool _isPlayerAuthoritative = false;
    
    [Signal]
    public delegate void WeaponFiredEventHandler();
    
    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("../Camera3D");
        
        // Create raycast for hit detection
        _rayCast = new RayCast3D();
        _rayCast.Enabled = false;
        AddChild(_rayCast);
        
        // Check if we have network authority
        var parent = GetParent();
        while (parent != null)
        {
            if (parent is PlayerController pc)
            {
                _isPlayerAuthoritative = pc.IsMultiplayerAuthority();
                break;
            }
            parent = parent.GetParent();
        }
    }
    
    public override void _Process(double delta)
    {
        if (!_isPlayerAuthoritative)
            return;
            
        // Update the timer
        if (!_canFire)
        {
            _timeSinceLastShot += delta;
            if (_timeSinceLastShot >= FireRate)
            {
                _canFire = true;
            }
        }
        
        // Handle firing
        if (_canFire && Input.IsActionPressed(FireActionName))
        {
            Fire();
        }
    }
    
    private void Fire()
    {
        if (_camera == null)
            return;
            
        // Reset the timer
        _timeSinceLastShot = 0;
        _canFire = false;
        
        // Ray from camera center
        var rayOrigin = _camera.GlobalPosition;
        var rayEnd = rayOrigin + _camera.GlobalTransform.Basis.Z * -Range;
        
        // Set up the raycast
        _rayCast.GlobalPosition = rayOrigin;
        _rayCast.TargetPosition = _camera.ToLocal(rayEnd);
        _rayCast.Enabled = true;
        _rayCast.ForceRaycastUpdate();
        
        // Check for hit
        if (_rayCast.IsColliding())
        {
            var collider = _rayCast.GetCollider();
            var collisionPoint = _rayCast.GetCollisionPoint();
            
            // Handle hit
            if (collider is PlayerController hitPlayer && IsMultiplayerAuthority())
            {
                // We hit a player, deal damage via RPC
                Rpc(nameof(TakeDamage), hitPlayer.GetPath(), Damage);
            }
            
            // Spawn impact effect
            if (BulletImpactEffect != null)
            {
                Rpc(nameof(SpawnImpactEffect), collisionPoint);
            }
        }
        
        // Clean up
        _rayCast.Enabled = false;
        
        // Signal that weapon was fired (for animations, sound, etc)
        EmitSignal(SignalName.WeaponFired);
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void TakeDamage(NodePath targetPath, int damage)
    {
        var target = GetNode<PlayerController>(targetPath);
        if (target != null)
        {
            // If we had a health component, we'd call it here
            GD.Print($"Player {target.PlayerId} took {damage} damage");
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void SpawnImpactEffect(Vector3 position)
    {
        if (BulletImpactEffect == null)
            return;
            
        var impact = BulletImpactEffect.Instantiate<Node3D>();
        GetTree().Root.AddChild(impact);
        impact.GlobalPosition = position;
        
        // Auto destroy after a delay
        var timer = new Timer();
        impact.AddChild(timer);
        timer.WaitTime = 2.0;
        timer.OneShot = true;
        timer.Timeout += () => impact.QueueFree();
        timer.Start();
    }
}