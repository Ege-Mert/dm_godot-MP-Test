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
    private bool _isLocalPlayer = false;

    public override void _EnterTree()
    {
        base._EnterTree();
        
        // Critical diagnostic message to track player authority
        GD.Print($"Player {Name} entered tree - authority: {GetMultiplayerAuthority()}, local ID: {Multiplayer.GetUniqueId()}, is authority: {Multiplayer.GetUniqueId() == GetMultiplayerAuthority()}");
    }

    public override void _Ready()
    {
        // Diagnostics about authority
        GD.Print($"Player {Name} ready, peer ID: {Multiplayer.GetUniqueId()}, Authority: {GetMultiplayerAuthority()}");
        
        _head = GetNode<Node3D>("Head");
        _collider = GetNode<CollisionShape3D>("Collider");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _mesh = GetNode<MeshInstance3D>("Mesh");
        
        var localMarker = GetNode<MeshInstance3D>("LocalMarker");
        var remoteMarker = GetNode<MeshInstance3D>("RemoteMarker");
        
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
        
        // CRITICAL FIX: Detect name/ID assignment disparity for multiplayer authority
        // The player's name is its ID assigned by the server
        long nameID = 0;
        if (long.TryParse(Name, out nameID))
        {
            // SPECIAL CASE HANDLING: 
            // If this player's name matches our peer ID, take control
            // OR if our peer ID is 1 (client) and this player's authority matches its name, take control
            bool isLocal = nameID == Multiplayer.GetUniqueId() || 
                          (Multiplayer.GetUniqueId() == 1 && nameID == GetMultiplayerAuthority());
            
            _isLocalPlayer = isLocal;
            
            GD.Print($"Player {PlayerId}: Special authority check: Name={nameID}, Peer={Multiplayer.GetUniqueId()}, Auth={GetMultiplayerAuthority()}");
            GD.Print($"Player {PlayerId}: Is Local Player = {isLocal}");
        
            // Set visual indicators for local vs remote
            if (localMarker != null)
                localMarker.Visible = isLocal;
            if (remoteMarker != null)
                remoteMarker.Visible = !isLocal;
            
            // Only enable input processing for the local player
            SetPhysicsProcess(isLocal);
            SetProcessInput(isLocal);
            SetProcessUnhandledInput(isLocal);
            
            // Update the network status display
            var mainScene = GetTree().CurrentScene;
            if (mainScene != null && isLocal)
            {
                var networkStatus = mainScene.GetNode<Label>("CanvasLayer/GameHUD/NetworkStatus");
                if (networkStatus != null)
                {
                    networkStatus.Text = $"Network ID: {Multiplayer.GetUniqueId()} (Node={Name})"; 
                }
            }
            
            // Setup camera and auto-capture mouse for local player
            if (_camera != null)
            {            
                if (isLocal)
                {
                    GD.Print($"Player {PlayerId}: Activating camera as local player");
                    
                    // Force make current with delay to ensure it becomes the active camera
                    CallDeferred(nameof(SetupLocalCamera));
                    
                    // Automatically capture mouse on start for the local player
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                    _mouseIsCaptured = true;
                    GD.Print($"Mouse captured during initialization");
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
                material.EmissionEnabled = true;
                material.Emission = new Color(0.3f, 0.0f, 0.0f); // Slight glow
                _mesh.MaterialOverride = material;
            }
            
            if (isLocal)
            {
                CallDeferred(nameof(ConnectToHUD));
            }
        }
        else
        {
            GD.PrintErr($"Player name {Name} is not a valid ID number!");
        }
    }
    
    private void SetupLocalCamera()
    {
        if (_camera != null)
        {
            GD.Print("Setting up local camera with force MakeCurrent");
            _camera.Current = true;
            _camera.ClearCurrent();
            _camera.MakeCurrent();
            
            // Make sure visibility mask includes everything
            _camera.CullMask = 0x7FFFFFFF;  // All bits set except the highest one
            _camera.Far = 1000.0f;  // Ensure far clipping plane is distant
            
            GD.Print("Camera setup complete");
        }
    }

    public override void _Input(InputEvent @event)
    {
        // CRITICAL: Only process input if this player is local
        if (!_isLocalPlayer)
        {
            return;
        }
            
        // Input handling in _Input gets priority over _UnhandledInput
        if (@event is InputEventMouseMotion mouseMotion)
        {
            // Process mouse movement here with high priority
            GD.Print($"Mouse motion detected: {mouseMotion.Relative}");
            RotateLook(mouseMotion.Relative);
            
            // Mark as handled to avoid double processing
            GetViewport().SetInputAsHandled();
        }
    }
    
    public override void _UnhandledInput(InputEvent @event)
    {
        // CRITICAL: Only process input if this player is local
        if (!_isLocalPlayer)
        {
            return;
        }
            
        // Mouse capturing - always capture at start
        if (Input.IsKeyPressed(Key.Escape))
        {
            ReleaseMouseCursor();
        }
        else if (!_mouseIsCaptured)
        {
            CaptureMouseCursor();
        }

        // Handle non-mouse-motion events here
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
        // CRITICAL: Only process physics if this player is local
        if (!_isLocalPlayer)
        {
            return;
        }
            
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
        // Check authority with direct comparison to ensure it works
        if (!_isLocalPlayer)
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
            
        // Double check we have multiplayer authority before processing mouse input
        if (!_isLocalPlayer)
            return;
            
        // Print every mouse input for debugging
        GD.Print($"Processing mouse movement: {rotInput}");
            
        // Apply mouse sensitivity
        float sensitivity = 0.005f; // Increased from default to make it more responsive
        _lookRotation.X -= rotInput.Y * sensitivity;
        _lookRotation.X = Mathf.Clamp(_lookRotation.X, Mathf.DegToRad(-85), Mathf.DegToRad(85));
        _lookRotation.Y -= rotInput.X * sensitivity;
        
        // Apply rotation directly to the nodes
        Basis bodyBasis = Basis.Identity;
        bodyBasis = bodyBasis.Rotated(Vector3.Up, _lookRotation.Y);
        Transform = new Transform3D(bodyBasis, Transform.Origin);
        
        Basis headBasis = Basis.Identity;
        headBasis = headBasis.Rotated(Vector3.Right, _lookRotation.X);
        _head.Transform = new Transform3D(headBasis, _head.Transform.Origin);
        
        // Debug output for every significant movement
        if (rotInput.Length() > 1.0f)
        {
            GD.Print($"Player {PlayerId}: Mouse rotation applied - body: {Rotation}, head: {_head.Rotation}");
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
    
    private async void ConnectToHUD()
    {
        try
        {
            // Give scene more time to initialize before connecting to HUD
            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
            
            GD.Print("Attempting to connect to HUD...");
            
            // Try all possible paths that might be used
            Control gameHUD = null;
            string[] possiblePaths = {
                "GameScene/CanvasLayer/GameHUD",
                "Node3D/CanvasLayer/GameHUD",
                "CanvasLayer/GameHUD",  // Maybe relative path
                "/root/GameScene/CanvasLayer/GameHUD"  // Absolute path
            };
            
            // Try to find HUD in any of these paths
            foreach (var path in possiblePaths)
            {
                try {
                    gameHUD = GetTree().Root.GetNode<Control>(path);
                    if (gameHUD != null) {
                        GD.Print($"Found HUD at path: {path}");
                        break;
                    }
                } catch {
                    // Silently fail and try next path
                }
            }
            
            // If not found, try getting from current scene
            if (gameHUD == null)
            {
                var currentScene = GetTree().CurrentScene;
                if (currentScene != null)
                {
                    try {
                        gameHUD = currentScene.GetNode<Control>("CanvasLayer/GameHUD");
                        if (gameHUD != null) {
                            GD.Print("Found HUD in current scene");
                        }
                    } catch {
                        // Final attempt failed
                    }
                }
            }
            
            if (gameHUD != null)
            {
                UpdateHUD(gameHUD);
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
    
    private void UpdateHUD(Control gameHUD = null)
    {
        try
        {
            // If no gameHUD passed, try to find it again
            if (gameHUD == null)
            {
                // Try different possible paths
                string[] possiblePaths = {
                    "GameScene/CanvasLayer/GameHUD",
                    "Node3D/CanvasLayer/GameHUD",
                    "/root/GameScene/CanvasLayer/GameHUD"
                };
                
                foreach (var path in possiblePaths)
                {
                    try {
                        gameHUD = GetTree().Root.GetNode<Control>(path);
                        if (gameHUD != null) break;
                    } catch {
                        // Try next path
                    }
                }
            }
            
            if (gameHUD == null) {
                GD.Print("Could not find GameHUD to update");
                return;
            }
            
            var healthBar = gameHUD.GetNode<ProgressBar>("%HealthBar");
            var healthLabel = gameHUD.GetNode<Label>("%HealthLabel");
            var killsLabel = gameHUD.GetNode<Label>("%KillsLabel");
            
            GD.Print($"Updating HUD - Health: {Health}, Kills: {Kills}, Deaths: {Deaths}");
            
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