using Godot;
using System;

public partial class PlayerController : CharacterBody3D
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

    // Networking properties
    [Export] public bool IsNetworkControlled { get; set; } = false;
    
    private bool _mouseIsCaptured = false;
    private Vector2 _lookRotation;
    private float _moveSpeed = 0.0f;
    private bool _isFreeflyEnabled = false;

    private Node3D _head;
    private CollisionShape3D _collider;
    private Camera3D _camera;
    private MeshInstance3D _mesh;

    // Multiplayer properties
    private MultiplayerSynchronizer _synchronizer;
    public int PlayerId { get; set; } = 0;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _collider = GetNode<CollisionShape3D>("Collider");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _mesh = GetNode<MeshInstance3D>("Mesh");
        
        _synchronizer = GetNode<MultiplayerSynchronizer>("MultiplayerSynchronizer");

        CheckInputMappings();
        _lookRotation.Y = Rotation.Y;
        _lookRotation.X = _head.Rotation.X;
        
        // Configure camera based on network status
        if (IsNetworkControlled && !IsMultiplayerAuthority())
        {
            _camera.Current = false;
        }
        else
        {
            _camera.Current = true;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Only process input if we're the owner of this player
        if (IsNetworkControlled && !IsMultiplayerAuthority())
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
    }

    public override void _PhysicsProcess(double delta)
    {
        // Only process physics if we're the owner of this player
        if (IsNetworkControlled && !IsMultiplayerAuthority())
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

    // Renamed from GetGravity to GetGravityValue to avoid hiding the base method
    private Vector3 GetGravityValue()
    {
        return ProjectSettings.GetSetting("physics/3d/default_gravity").As<float>() * Vector3.Down;
    }

    private void RotateLook(Vector2 rotInput)
    {
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