using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
    [Export] public string ServerIP { get; set; } = "127.0.0.1";
    [Export] public int ServerPort { get; set; } = 28960;
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public string GameScenePath { get; set; } = "res://scenes/GameScene.tscn";
    
    private ENetMultiplayerPeer _peer;
    private Dictionary<long, Node> _players = new Dictionary<long, Node>();
    private bool _isServer = false;
    private bool _sceneLoaded = false;
    private Label _debugLabel;
    
    [Signal]
    public delegate void ServerStartedEventHandler();
    
    [Signal]
    public delegate void ClientConnectedEventHandler();
    
    [Signal]
    public delegate void NetworkErrorEventHandler(string error);
    
    [Signal]
    public delegate void GameSceneReadyEventHandler();
    
    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        
        // Create debug UI immediately
        CreateDebugUI();
        
        if (PlayerScene == null)
        {
            // Try to load the player scene if not set
            PlayerScene = ResourceLoader.Load<PackedScene>("res://scenes/NetworkedPlayer.tscn");
            if (PlayerScene == null)
            {
                GD.PrintErr("PlayerScene not set in NetworkManager and could not be loaded automatically");
            }
            else
            {
                GD.Print("Player scene loaded automatically");
            }
        }
        
        // Connect to scene tree changes to detect when game scene is fully loaded
        GetTree().NodeAdded += OnNodeAdded;
    }
    
    private void CreateDebugUI()
    {
        _debugLabel = new Label();
        _debugLabel.Position = new Vector2(20, 80);
        _debugLabel.Text = "NetworkManager initialized";
        
        var canvas = new CanvasLayer();
        canvas.Name = "NetworkDebugLayer";
        canvas.Layer = 10; // High layer number to be on top
        canvas.AddChild(_debugLabel);
        
        AddChild(canvas);
    }
    
    private void UpdateDebugLabel()
    {
        if (_debugLabel == null) return;
        
        string status = $"Network Status:\n";
        status += $"Mode: {(_isServer ? "Server" : "Client")}\n";
        status += $"My ID: {Multiplayer.GetUniqueId()}\n";
        status += $"Scene Loaded: {_sceneLoaded}\n";
        status += $"Players: {_players.Count}\n";
        
        foreach (var player in _players)
        {
            status += $"  - Player {player.Key} (Path: {player.Value?.GetPath() ?? "null"})\n";
        }
        
        _debugLabel.Text = status;
    }
    
    private void OnNodeAdded(Node node)
    {
        // Improved scene detection
        if (!_sceneLoaded && GetTree().CurrentScene != null)
        {
            var currentScenePath = GetTree().CurrentScene.SceneFilePath;
            GD.Print($"Current scene: {currentScenePath}, Checking against: {GameScenePath}");
            
            if (currentScenePath == GameScenePath)
            {
                GD.Print("Game scene detected as loaded!");
                _sceneLoaded = true;
                
                // Wait a moment for scene to fully initialize before spawning players
                var timer = new Timer();
                timer.WaitTime = 0.5f;
                timer.OneShot = true;
                timer.Timeout += () => {
                    GD.Print("Scene initialization timer complete, ready to spawn players");
                    EmitSignal(SignalName.GameSceneReady);
                    
                    // If we're the server, spawn all currently connected players including ourselves
                    if (_isServer)
                    {
                        SpawnAllPlayers();
                    }
                    
                    timer.QueueFree();
                };
                AddChild(timer);
                timer.Start();
                
                // Also add debug visualization to game scene
                AddDebugVisualsToScene();
            }
        }
        
        UpdateDebugLabel();
    }
    
    private void AddDebugVisualsToScene()
    {
        // Add a visible landmark to help debug
        var sphere = new CsgSphere3D();
        sphere.Name = "DebugSphere";
        sphere.Radius = 1.0f;
        sphere.Position = new Vector3(0, 5, 0);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1, 0, 1); // Magenta
        material.Emission = new Color(1, 0, 1); // Make it glow
        material.EmissionEnergyMultiplier = 0.7f;
        sphere.MaterialOverride = material;
        
        GetTree().CurrentScene.AddChild(sphere);
        
        // Add debug text in 3D space
        var label3D = new Label3D();
        label3D.Text = $"Game Scene\nServer: {_isServer}\nID: {Multiplayer.GetUniqueId()}";
        label3D.Position = new Vector3(0, 7, 0);
        label3D.FontSize = 48;
        label3D.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        label3D.NoDepthTest = true; // Always visible
        label3D.Modulate = new Color(1, 1, 0); // Yellow for visibility
        GetTree().CurrentScene.AddChild(label3D);
        
        // Add debug boxes in each corner of the scene
        AddCornerMarker(new Vector3(-10, 1, -10), new Color(1, 0, 0)); // Red
        AddCornerMarker(new Vector3(-10, 1, 10), new Color(0, 1, 0)); // Green
        AddCornerMarker(new Vector3(10, 1, -10), new Color(0, 0, 1)); // Blue
        AddCornerMarker(new Vector3(10, 1, 10), new Color(1, 1, 0)); // Yellow
    }
    
    private void AddCornerMarker(Vector3 position, Color color)
    {
        var box = new CsgBox3D();
        box.Size = new Vector3(2, 2, 2);
        box.Position = position;
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = color;
        material.Emission = color;
        material.EmissionEnergyMultiplier = 0.5f;
        box.MaterialOverride = material;
        
        var label = new Label3D();
        label.Text = $"Corner\n{position.X}, {position.Y}, {position.Z}";
        label.Position = new Vector3(0, 2, 0);
        label.FontSize = 24;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        box.AddChild(label);
        
        GetTree().CurrentScene.AddChild(box);
    }
    
    public void StartServer()
    {
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateServer(ServerPort, 32);
        
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to start server: {error}");
            EmitSignal(SignalName.NetworkError, $"Failed to start server: {error}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = _peer;
        _isServer = true;
        GD.Print($"Server started on port {ServerPort}");
        
        EmitSignal(SignalName.ServerStarted);
        
        // Load the game scene
        GetTree().ChangeSceneToFile(GameScenePath);
        UpdateDebugLabel();
    }
    
    public void JoinServer()
    {
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateClient(ServerIP, ServerPort);
        
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to connect to server: {error}");
            EmitSignal(SignalName.NetworkError, $"Failed to connect to server: {error}");
            return;
        }
        
        Multiplayer.MultiplayerPeer = _peer;
        _isServer = false;
        GD.Print($"Connected to server at {ServerIP}:{ServerPort}");
        
        EmitSignal(SignalName.ClientConnected);
        
        // Load the game scene
        GetTree().ChangeSceneToFile(GameScenePath);
        UpdateDebugLabel();
    }
    
    public void Disconnect()
    {
        if (_peer != null)
        {
            _peer.Close();
            _peer = null;
        }
        
        _players.Clear();
        _isServer = false;
        _sceneLoaded = false;
        
        // Return to the main menu
        GetTree().ChangeSceneToFile("res://scenes/UI/MainMenu.tscn");
        UpdateDebugLabel();
    }
    
    private void OnPeerConnected(long id)
    {
        GD.Print($"Player connected: {id}");
        
        // Server spawns player for new connections, but only if scene is loaded
        if (_isServer && _sceneLoaded)
        {
            SpawnPlayer(id);
        }
        
        UpdateDebugLabel();
    }
    
    private void OnPeerDisconnected(long id)
    {
        GD.Print($"Player disconnected: {id}");
        
        if (_players.ContainsKey(id))
        {
            if (_players[id] != null && IsInstanceValid(_players[id]))
            {
                _players[id].QueueFree();
            }
            
            _players.Remove(id);
        }
        
        UpdateDebugLabel();
    }
    
    private void SpawnAllPlayers()
    {
        GD.Print("Spawning all currently connected players");
        
        // Server ID is always 1
        SpawnPlayer(1);
        
        // Spawn all connected peers
        foreach (long id in Multiplayer.GetPeers())
        {
            SpawnPlayer(id);
        }
        
        UpdateDebugLabel();
    }
    
    public void SpawnPlayer(long id)
    {
        GD.Print($"Attempting to spawn player for peer: {id}");
        
        if (PlayerScene == null)
        {
            GD.PrintErr("PlayerScene is null in NetworkManager");
            return;
        }
        
        // Don't spawn if player already exists
        if (_players.ContainsKey(id) && _players[id] != null && IsInstanceValid(_players[id]))
        {
            GD.Print($"Player {id} already spawned, not spawning again");
            return;
        }
        
        // Instance the player scene
        var player = PlayerScene.Instantiate<Node3D>();
        player.Name = id.ToString();
        
        // Set network authority BEFORE adding to the scene
        player.SetMultiplayerAuthority((int)id);
        
        // Find spawn points
        var spawnPoints = GetTree().GetNodesInGroup("SpawnPoints");
        GD.Print($"Found {spawnPoints.Count} spawn points in 'SpawnPoints' group");
        
        if (spawnPoints.Count == 0)
        {
            spawnPoints = GetTree().GetNodesInGroup("Spawnpoints");
            GD.Print($"Found {spawnPoints.Count} spawn points in 'Spawnpoints' group");
        }
        
        // Add the player to the scene
        GetTree().CurrentScene.AddChild(player);
        
        // DEBUG: Check authority
        GD.Print($"Player {id} added to scene - Authority check: {player.GetMultiplayerAuthority()}, IsAuthority: {player.IsMultiplayerAuthority()}");
        
        // Set player position
        if (spawnPoints.Count > 0)
        {
            var spawnIndex = GD.Randi() % (uint)spawnPoints.Count;
            var spawnPoint = spawnPoints[(int)spawnIndex] as Node3D;
            
            if (spawnPoint != null)
            {
                player.GlobalPosition = spawnPoint.GlobalPosition;
                // Slightly raise the player to avoid clipping with floor
                player.GlobalPosition += new Vector3(0, 1, 0);
                GD.Print($"Spawning player at position: {player.GlobalPosition}");
            }
            else
            {
                GD.PrintErr("Spawn point is not a Node3D!");
                player.GlobalPosition = new Vector3(0, 2, 0);
            }
        }
        else
        {
            GD.PrintErr("No spawn points found!");
            player.GlobalPosition = new Vector3(0, 2, 0);
            GD.Print("Using default spawn position");
        }
        
        // Now add to our tracking dictionary
        _players[id] = player;
        
        GD.Print($"Spawned player for peer: {id} at position {player.GlobalPosition}");
        UpdateDebugLabel();
    }
    
    public bool IsHost()
    {
        return _isServer;
    }
    
    public Dictionary<long, Node> GetPlayers()
    {
        return _players;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Continuously update player count in debug label
        UpdateDebugLabel();
        
        // Scan for camera issues
        if (_sceneLoaded && GetTree().CurrentScene != null)
        {
            // Find all cameras in the scene
            var allCameras = GetTree().GetNodesInGroup("Cameras");
            bool anyCameraActive = false;
            
            foreach (var node in allCameras)
            {
                if (node is Camera3D camera && camera.Current)
                {
                    anyCameraActive = true;
                    break;
                }
            }
            
            if (!anyCameraActive && _players.Count > 0)
            {  
                GD.Print("No active camera found, trying to force cameras...");
            }
        }
    }
}