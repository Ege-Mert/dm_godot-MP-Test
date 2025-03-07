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
    [Export] public string InputLeft { get; set; } = "move_left";
    [Export] public string InputRight { get; set; } = "move_right";
    [Export] public string InputForward { get; set; } = "move_up";
    [Export] public string InputBack { get; set; } = "move_down";
    [Export] public string InputJump { get; set; } = "jump";
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

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _collider = GetNode<CollisionShape3D>("Collider");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _mesh = GetNode<MeshInstance3D>("Mesh");
        
        if (!SynchronizerPath.IsEmpty)
        {
            _synchronizer = GetNode<MultiplayerSynchronizer>(SynchronizerPath);
            if (_synchronizer != null)
            {
                GD.Print($"Player {GetMultiplayerAuthority()}: Found MultiplayerSynchronizer");
            }
            else
            {
                GD.PrintErr($"Player {GetMultiplayerAuthority()}: MultiplayerSynchronizer not found at path: {SynchronizerPath}");
            }
        }
        
        PlayerId = (int)GetMultiplayerAuthority();
        GD.Print($"Player {PlayerId} spawned, authority: {GetMultiplayerAuthority()}, local: {Multiplayer.GetUniqueId()}");

        CheckInputMappings();
        _lookRotation.Y = Rotation.Y;
        _lookRotation.X = _head.Rotation.X;
        
        // Determine if this is the local player
        bool isLocal = IsMultiplayerAuthority();
        GD.Print($"Player {PlayerId}: Is Local Player = {isLocal}");
        
        // Only enable input processing for the local player
        SetPhysicsProcess(isLocal);
        SetProcessInput(isLocal);
        
        // Setup camera and auto-capture mouse for local player
        if (_camera != null)
        {            
            if (isLocal)
            {
                GD.Print($"Player {PlayerId}: Activating camera as local player");
                _camera.Current = true;
                // Force camera to be enabled to improve visibility
                _camera.Visible = true;
                _camera.ClearCurrent();
                _camera.MakeCurrent();
                
                // Automatically capture mouse on start for the local player
                Input.MouseMode = Input.MouseModeEnum.Captured;
                _mouseIsCaptured = true;
            }
            else
            {
                _camera.Current = false;
            }
        }
        
        // Set a different color for remote players
        if (!isLocal && _mesh != null)
        {
            var material = new StandardMaterial3D();
            material.AlbedoColor = new Color(1.0f, 0.0f, 0.0f); // Red for remote players
            _mesh.MaterialOverride = material;
        }
        
        if (isLocal)
        {
            CallDeferred(nameof(ConnectToHUD));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Only process input if we're the owner of this player
        if (!IsMultiplayerAuthority())
            return;
            
        // Mouse capturing - always capture at start
        if (Input.IsKeyPressed(Key.Escape))
        {
            ReleaseMouseCursor();
        }
        else if (!_mouseIsCaptured)
        {
            CaptureMouseCursor();
        }

        // Look around - this is critical for the game
        if (@event is InputEventMouseMotion mouseMotion)
        {
            // Always rotate look even if mouse isn't explicitly captured
            // This helps ensure mouse look works in all cases
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
        
        // Force re-capture if we detect input but mouse isn't captured
        // This helps ensure the mouse is always captured during gameplay
        if (!_mouseIsCaptured && !Input.IsKeyPressed(Key.Escape))
        {
            CaptureMouseCursor();
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
        if (spawnPoints.Count > 0)
        {
            var spawnIndex = new Random().Next(0, spawnPoints.Count);
            GlobalPosition = (spawnPoints[spawnIndex] as Node3D).GlobalPosition;
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void AddKill()
    {
        Kills++;
    }
    
    private void Fire()
    {
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
        // Skip tiny movements that might be noise
        if (rotInput.LengthSquared() < 0.01f)
            return;
            
        _lookRotation.X -= rotInput.Y * LookSpeed;
        _lookRotation.X = Mathf.Clamp(_lookRotation.X, Mathf.DegToRad(-85), Mathf.DegToRad(85));
        _lookRotation.Y -= rotInput.X * LookSpeed;
        
        // Make sure we reset the basis before applying rotation to avoid accumulation errors
        Transform = new Transform3D(Basis.Identity, Transform.Origin);
        RotateY(_lookRotation.Y);
        _head.Transform = new Transform3D(Basis.Identity, _head.Transform.Origin);
        _head.RotateX(_lookRotation.X);
        
        // Debug output to confirm rotation is happening
        if (rotInput.Length() > 5.0f) // Only log significant movements to avoid spam
        {
            GD.Print($"Player {PlayerId}: Mouse rotation input: {rotInput}, Head rotation: {_head.Rotation}");
        }
    }

    private void EnableFreefly()
    {
        _collider.Disabled = true;
        _isFreeflyEnabled = true;
        Velocity = Vector3.Zero;
    }

    private void DisableFreefly()
    {
        _collider.Disabled = false;
        _isFreeflyEnabled = false;
    }

    private void CaptureMouseCursor()
    {
        // Force mouse capture and print debug info
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseIsCaptured = true;
        GD.Print($"Player {PlayerId}: Mouse cursor captured");
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
    
    private void ConnectToHUD()
    {
        try
        {
            // Try different possible paths to find the GameHUD
            var gameHUD = GetTree().Root.GetNode<Control>("Node3D/CanvasLayer/GameHUD");
            if (gameHUD == null)
            {
                // Try alternative path naming
                gameHUD = GetTree().Root.GetNode<Control>("GameScene/CanvasLayer/GameHUD");
            }
            
            if (gameHUD != null)
            {
                UpdateHUD();
                GD.Print($"Player {PlayerId}: Connected to HUD");
            }
            else
            {
                GD.Print($"Player {PlayerId}: Could not find GameHUD to connect to");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Player {PlayerId}: Error connecting to HUD: {e.Message}");
        }
    }
    
    private void UpdateHUD()
    {
        try
        {
            // Try different possible paths to find the GameHUD
            var gameHUD = GetTree().Root.GetNode<Control>("Node3D/CanvasLayer/GameHUD");
            if (gameHUD == null)
            {
                // Try alternative path naming
                gameHUD = GetTree().Root.GetNode<Control>("GameScene/CanvasLayer/GameHUD");
            }
            
            if (gameHUD == null) return;
            
            var healthBar = gameHUD.GetNode<ProgressBar>("%HealthBar");
            var healthLabel = gameHUD.GetNode<Label>("%HealthLabel");
            var killsLabel = gameHUD.GetNode<Label>("%KillsLabel");
            
            if (healthBar != null)
            {
                healthBar.Value = Health;
                healthBar.MaxValue = 100;
            }
            
            if (healthLabel != null)
            {
                healthLabel.Text = $"Health: {Health}/100";
            }
            
            if (killsLabel != null)
            {
                killsLabel.Text = $"Kills: {Kills} | Deaths: {Deaths}";
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Player {PlayerId}: Error updating HUD: {e.Message}");
        }
    }
}