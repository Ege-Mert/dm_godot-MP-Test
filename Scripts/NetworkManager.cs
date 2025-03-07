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
    
    private void OnNodeAdded(Node node)
    {
        // When we detect our game scene is loaded, we can spawn players
        if (!_sceneLoaded && node.Name == GetGameSceneName())
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
                
                // If we're the server, spawn our own player
                if (_isServer)
                {
                    SpawnAllPlayers();
                }
                
                timer.QueueFree();
            };
            AddChild(timer);
            timer.Start();
        }
    }
    
    private string GetGameSceneName()
    {
        // Extract scene name from path
        var parts = GameScenePath.Split('/');
        if (parts.Length > 0)
        {
            var lastPart = parts[parts.Length - 1];
            return lastPart.Replace(".tscn", "");
        }
        return "GameScene";
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
    }
    
    private void OnPeerConnected(long id)
    {
        GD.Print($"Player connected: {id}");
        
        // Server spawns player for new connections, but only if scene is loaded
        if (_isServer && _sceneLoaded)
        {
            SpawnPlayer(id);
        }
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
    }
    
    private void SpawnAllPlayers()
    {
        GD.Print("Spawning all currently connected players");
        
        // Spawn all currently connected peers including the server (peer ID 1)
        SpawnPlayer(1);
        
        foreach (long id in Multiplayer.GetPeers())
        {
            SpawnPlayer(id);
        }
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
        
        // Set network authority
        player.SetMultiplayerAuthority((int)id);
        
        // Find spawn points
        var spawnPoints = GetTree().GetNodesInGroup("SpawnPoints");
        if (spawnPoints.Count == 0)
        {
            // Try lowercase 'p' if uppercase 'P' doesn't work
            spawnPoints = GetTree().GetNodesInGroup("Spawnpoints");
        }
        
        GD.Print($"Found {spawnPoints.Count} spawn points");
        
        // First add the player to the scene
        GetTree().CurrentScene.AddChild(player);
        
        // ONLY NOW (after adding to scene) set the position
        if (spawnPoints.Count > 0)
        {
            var spawnIndex = GD.Randi() % (uint)spawnPoints.Count;
            var spawnPoint = spawnPoints[(int)spawnIndex] as Node3D;
            
            if (spawnPoint != null)
            {
                // Now it's safe to get the global position
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
            GD.PrintErr("No spawn points found in either 'SpawnPoints' or 'Spawnpoints' group!");
            // Default spawn position - a bit elevated to avoid falling through floor
            player.GlobalPosition = new Vector3(0, 2, 0);
            GD.Print("Using default spawn position");
        }
        
        // Now add to our tracking dictionary
        _players[id] = player;
        
        GD.Print($"Spawned player for peer: {id} at position {player.GlobalPosition}");
    }
    
    public bool IsHost()
    {
        return _isServer;
    }
    
    public Dictionary<long, Node> GetPlayers()
    {
        return _players;
    }
}