using Godot;
using System;

public partial class NetworkedPlayer : CharacterBody3D
{
    // Configuration flags
    [Export] public bool CanMove { get; set; } = true;
    [Export] public bool HasGravity { get; set; } = true;
    [Export] public bool CanJump { get; set; } = true;
    [Export] public bool CanSprint { get; set; } = false;
    [Export] public bool CanFreefly { get; set; } = false;

    // Speed settings
    [Export] public float LookSpeed { get; set; } = 0.002f;
    [Export] public float BaseSpeed { get; set; } = 7.0f;
    [Export] public float JumpVelocity { get; set; } = 4.5f;
    [Export] public float SprintSpeed { get; set; } = 10.0f;
    [Export] public float FreeflySpeed { get; set; } = 25.0f;

    // Input actions
    [Export] public string InputLeft { get; set; } = "ui_left";
    [Export] public string InputRight { get; set; } = "ui_right";
    [Export] public string InputForward { get; set; } = "ui_up";
    [Export] public string InputBack { get; set; } = "ui_down";
    [Export] public string InputJump { get; set; } = "ui_accept";
    [Export] public string InputSprint { get; set; } = "sprint";
    [Export] public string InputFreefly { get; set; } = "freefly";
    [Export] public string InputFire { get; set; } = "fire";

    // Networking properties
    [Export] public NodePath SynchronizerPath { get; set; }
    
    // Player stats
    [Export] public int Health { get; set; } = 100;
    [Export] public int Kills { get; set; } = 0;
    [Export] public int Deaths { get; set; } = 0;
    public int PlayerId { get; set; } = 0;
    
    private bool _mouseIsCaptured = false;
    private Vector2 _lookRotation;
    private float _moveSpeed = 0.0f;
    private bool _isFreeflyEnabled = false;

    private Node3D _head;
    private CollisionShape3D _collider;
    private Camera3D _camera;
    private MeshInstance3D _mesh;
    private MultiplayerSynchronizer _synchronizer;
    
    // Debug label
    private Label3D _debugLabel;

    public override void _Ready()
    {
        // Print debug info
        GD.Print($"NetworkedPlayer initializing. Name: {Name}");
        
        // Add debug label
        _debugLabel = new Label3D();
        _debugLabel.Text = $"Player {GetMultiplayerAuthority()}";
        _debugLabel.Position = new Vector3(0, 2, 0);
        //_debugLabel.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        _debugLabel.FontSize = 24;
        _debugLabel.Modulate = new Color(1, 1, 0); // Yellow
        AddChild(_debugLabel);
        
        // Find required nodes
        _head = GetNodeOrNull<Node3D>("Head");
        _collider = GetNodeOrNull<CollisionShape3D>("Collider");
        _camera = GetNodeOrNull<Camera3D>("Head/Camera3D");
        _mesh = GetNodeOrNull<MeshInstance3D>("Mesh");
        
        if (!SynchronizerPath.IsEmpty)
        {
            _synchronizer = GetNodeOrNull<MultiplayerSynchronizer>(SynchronizerPath);
        }
        
        // Debug output for node references
        GD.Print($"Player {Name} - Head: {(_head != null)}, Camera: {(_camera != null)}, Mesh: {(_mesh != null)}");
        
        PlayerId = (int)GetMultiplayerAuthority();

        if (_head != null && _camera != null)
        {
            GD.Print($"Player {Name} - Setting up camera, IsAuthority: {IsMultiplayerAuthority()}");
            
            // Only make the local player's camera active
            _camera.Current = IsMultiplayerAuthority();
            
            // Make the camera more visible in debug
            if (_camera.Current)
            {
                GD.Print($"Player {Name} - Camera activated");
                
                // Set visible debug camerainfo
                _debugLabel.Text += " (YOU)";
                _debugLabel.Modulate = new Color(0, 1, 0); // Green for local player
            }
        }
        else
        {
            GD.PrintErr($"Player {Name} - Missing head or camera node!");
        }
        
        // Setup mesh for remote players
        if (!IsMultiplayerAuthority() && _mesh != null)
        {
            var material = new StandardMaterial3D();
            material.AlbedoColor = new Color(1.0f, 0.0f, 0.0f); // Red for remote players
            _mesh.MaterialOverride = material;
        }
        
        CheckInputMappings();
        
        if (_head != null)
        {
            _lookRotation.Y = Rotation.Y;
            _lookRotation.X = _head.Rotation.X;
        }
        
        // Only enable input processing for the local player
        SetPhysicsProcess(IsMultiplayerAuthority());
        SetProcessInput(IsMultiplayerAuthority());
        
        AddToGroup("Players");
        
        GD.Print($"Player {Name} initialized at position {GlobalPosition}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Only process input if we're the owner of this player
        if (!IsMultiplayerAuthority())
            return;
            
        // Mouse capturing
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            CaptureMouseCursor();
        }
        if (Input.IsKeyPressed(Key.Escape))
        {
            ReleaseMouseCursor();
        }

        // Look around
        if (_mouseIsCaptured && @event is InputEventMouseMotion mouseMotion)
        {
            RotateLook(mouseMotion.Relative);
        }

        // Toggle freefly mode
        if (CanFreefly && Input.IsActionJustPressed(InputFreefly))
        {
            if (!_isFreeflyEnabled)
            {
                EnableFreefly();
            }
            else
            {
                DisableFreefly();
            }
        }
        
        // Handle shooting
        if (Input.IsActionJustPressed(InputFire))
        {
            Fire();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Only process physics if we're the owner of this player
        if (!IsMultiplayerAuthority())
            return;
            
        // If freeflying, handle freefly and nothing else
        if (CanFreefly && _isFreeflyEnabled)
        {
            var inputDir = Input.GetVector(InputLeft, InputRight, InputForward, InputBack);
            var motion = (_head.GlobalBasis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
            motion *= FreeflySpeed * (float)delta;
            MoveAndCollide(motion);
            return;
        }

        // Apply gravity to velocity
        if (HasGravity)
        {
            if (!IsOnFloor())
            {
                Velocity += GetGravityValue() * (float)delta;
            }
        }

        // Apply jumping
        if (CanJump)
        {
            if (Input.IsActionJustPressed(InputJump) && IsOnFloor())
            {
                Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
            }
        }

        // Modify speed based on sprinting
        if (CanSprint && Input.IsActionPressed(InputSprint))
        {
            _moveSpeed = SprintSpeed;
        }
        else
        {
            _moveSpeed = BaseSpeed;
        }

        // Apply desired movement to velocity
        if (CanMove)
        {
            var inputDir = Input.GetVector(InputLeft, InputRight, InputForward, InputBack);
            var moveDir = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
            if (moveDir != Vector3.Zero)
            {
                Velocity = new Vector3(moveDir.X * _moveSpeed, Velocity.Y, moveDir.Z * _moveSpeed);
            }
            else
            {
                Velocity = new Vector3(Mathf.MoveToward(Velocity.X, 0, _moveSpeed), Velocity.Y, Mathf.MoveToward(Velocity.Z, 0, _moveSpeed));
            }
        }
        else
        {
            Velocity = new Vector3(0, Velocity.Y, 0);
        }

        // Use velocity to actually move
        MoveAndSlide();
        
        // Update debug label position to stay above player
        if (_debugLabel != null)
        {
            _debugLabel.Text = $"Player {PlayerId} H:{Health} K:{Kills} D:{Deaths}";
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority)]
    public void TakeDamage(int amount, int attackerId)
    {
        if (!IsMultiplayerAuthority())
            return;
            
        Health -= amount;
        
        if (Health <= 0)
        {
            Health = 0;
            Die(attackerId);
        }
    }
    
    private void Die(int killerId)
    {
        Deaths++;
        
        // Inform the killer they got a kill
        if (killerId != PlayerId)
        {
            RpcId(killerId, nameof(AddKill));
        }
        
        // Reset health and respawn
        Health = 100;
        
        // Find a spawn point
        var spawnPoints = GetTree().GetNodesInGroup("SpawnPoints");
        if (spawnPoints.Count == 0)
        {
            spawnPoints = GetTree().GetNodesInGroup("Spawnpoints");
        }
        
        if (spawnPoints.Count > 0)
        {
            var spawnIndex = new Random().Next(0, spawnPoints.Count);
            var spawnPoint = spawnPoints[spawnIndex] as Node3D;
            if (spawnPoint != null)
            {
                GlobalPosition = spawnPoint.GlobalPosition + new Vector3(0, 1, 0);
            }
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void AddKill()
    {
        Kills++;
    }
    
    private void Fire()
    {
        if (_camera == null)
        {
            GD.PrintErr($"Player {Name} - Cannot fire, camera is null");
            return;
        }
            
        // Create a raycast from the camera
        var rayLength = 1000.0f;
        var rayFrom = _camera.GlobalPosition;
        var rayTo = rayFrom + _camera.GlobalTransform.Basis.Z * -rayLength;
        
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayTo);
        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            var collider = result["collider"].As<Node>();
            if (collider != null && collider is NetworkedPlayer hitPlayer)
            {
                // Don't hit yourself
                if (hitPlayer.PlayerId != PlayerId)
                {
                    // Deal damage to the hit player
                    hitPlayer.RpcId(hitPlayer.GetMultiplayerAuthority(), nameof(TakeDamage), 25, PlayerId);
                    GD.Print($"Hit player {hitPlayer.PlayerId}");
                }
            }
        }
    }

    // Avoid hiding the base method with 'new'
    private Vector3 GetGravityValue()
    {
        return ProjectSettings.GetSetting("physics/3d/default_gravity").As<float>() * Vector3.Down;
    }

    private void RotateLook(Vector2 rotInput)
    {
        if (_head == null) return;
        
        _lookRotation.X -= rotInput.Y * LookSpeed;
        _lookRotation.X = Mathf.Clamp(_lookRotation.X, Mathf.DegToRad(-85), Mathf.DegToRad(85));
        _lookRotation.Y -= rotInput.X * LookSpeed;
        Transform = new Transform3D(Basis.Identity, Transform.Origin);
        RotateY(_lookRotation.Y);
        _head.Transform = new Transform3D(Basis.Identity, _head.Transform.Origin);
        _head.RotateX(_lookRotation.X);
    }

    private void EnableFreefly()
    {
        if (_collider != null)
        {
            _collider.Disabled = true;
        }
        _isFreeflyEnabled = true;
        Velocity = Vector3.Zero;
    }

    private void DisableFreefly()
    {
        if (_collider != null)
        {
            _collider.Disabled = false;
        }
        _isFreeflyEnabled = false;
    }

    private void CaptureMouseCursor()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseIsCaptured = true;
    }

    private void ReleaseMouseCursor()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _mouseIsCaptured = false;
    }

    private void CheckInputMappings()
    {
        if (CanMove && !InputMap.HasAction(InputLeft))
        {
            GD.PushError($"Movement disabled. No InputAction found for InputLeft: {InputLeft}");
            CanMove = false;
        }
        if (CanMove && !InputMap.HasAction(InputRight))
        {
            GD.PushError($"Movement disabled. No InputAction found for InputRight: {InputRight}");
            CanMove = false;
        }
        if (CanMove && !InputMap.HasAction(InputForward))
        {
            GD.PushError($"Movement disabled. No InputAction found for InputForward: {InputForward}");
            CanMove = false;
        }
        if (CanMove && !InputMap.HasAction(InputBack))
        {
            GD.PushError($"Movement disabled. No InputAction found for InputBack: {InputBack}");
            CanMove = false;
        }
        if (CanJump && !InputMap.HasAction(InputJump))
        {
            GD.PushError($"Jumping disabled. No InputAction found for InputJump: {InputJump}");
            CanJump = false;
        }
        if (CanSprint && !InputMap.HasAction(InputSprint))
        {
            GD.PushError($"Sprinting disabled. No InputAction found for InputSprint: {InputSprint}");
            CanSprint = false;
        }
        if (CanFreefly && !InputMap.HasAction(InputFreefly))
        {
            GD.PushError($"Freefly disabled. No InputAction found for InputFreefly: {InputFreefly}");
            CanFreefly = false;
        }
    }
}