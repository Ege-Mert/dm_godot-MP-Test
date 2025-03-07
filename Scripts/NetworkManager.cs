using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
    [Export] public string ServerIP { get; set; } = "127.0.0.1";
    [Export] public int ServerPort { get; set; } = 28960;
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public string GameScenePath { get; set; } = "res://Scenes/GameScene.tscn";
    
    private ENetMultiplayerPeer _peer;
    private Dictionary<long, Node> _players = new Dictionary<long, Node>();
    private bool _isServer = false;
    
    [Signal]
    public delegate void ServerStartedEventHandler();
    
    [Signal]
    public delegate void ClientConnectedEventHandler();
    
    [Signal]
    public delegate void NetworkErrorEventHandler(string error);
    
    [Signal]
    public delegate void PeerConnectedEventHandler(long id);
    
    [Signal]
    public delegate void PeerDisconnectedEventHandler(long id);
    
    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        
        // Load player scene if not already set
        if (PlayerScene == null)
        {
            string playerScenePath = "res://scenes/NetworkedPlayer.tscn";
            PlayerScene = ResourceLoader.Load<PackedScene>(playerScenePath);
            if (PlayerScene == null)
            {
                GD.PrintErr($"Could not load NetworkedPlayer scene from {playerScenePath}");
            }
            else
            {
                GD.Print($"Successfully loaded PlayerScene from {playerScenePath}");
            }
        }
        else
        {
            GD.Print("PlayerScene already set in NetworkManager");
        }
        
        // Validate game scene path
        if (!ResourceLoader.Exists(GameScenePath))
        {
            string defaultPath = "res://scenes/GameScene.tscn";
            if (ResourceLoader.Exists(defaultPath))
            {
                GD.PrintErr($"Game scene not found at {GameScenePath}, using default path {defaultPath}");
                GameScenePath = defaultPath;
            }
        }
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
        
        // Make sure host player is spawned after scene changes
        CallDeferred(nameof(SpawnHostPlayer));
    }
    
    private void SpawnHostPlayer()
    {
        if (_isServer)
        {
            GD.Print("Spawning host player (ID 1)...");
            // Add a small delay before spawning to ensure scene is ready
            var timer = new Timer();
            timer.WaitTime = 0.5f; // Half-second delay
            timer.OneShot = true;
            AddChild(timer);
            timer.Timeout += () => {
                GD.Print("Timer expired, now spawning host player");
                SpawnPlayer(1);
                timer.QueueFree();
            };
            timer.Start();
        }
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
        
        // Return to the main menu
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }
    
    private void OnPeerConnected(long id)
    {
        GD.Print($"Player connected: {id}");
        
        // Server spawns player for new connections
        if (_isServer)
        {
            GD.Print($"Server is spawning player for peer: {id}");
            // Use CallDeferred to ensure the scene is fully loaded
            CallDeferred(nameof(SpawnPlayer), id);
        }
        
        // Emit signal for other systems to react
        EmitSignal(SignalName.PeerConnected, id);
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
        
        // Emit signal for other systems to react
        EmitSignal(SignalName.PeerDisconnected, id);
    }
    
    public void SpawnPlayer(long id)
    {
        // Try to load the player scene if not already set
        if (PlayerScene == null)
        {
            // Try to load the player scene again as a last resort
            string playerScenePath = "res://scenes/NetworkedPlayer.tscn";
            PlayerScene = ResourceLoader.Load<PackedScene>(playerScenePath);
            
            if (PlayerScene == null)
            {
                GD.PrintErr("Player scene not set in NetworkManager and could not be loaded");
                EmitSignal(SignalName.NetworkError, "Could not spawn player: player scene missing");
                return;
            }
        }
        
        // Don't spawn if player already exists
        if (_players.ContainsKey(id) && _players[id] != null && IsInstanceValid(_players[id]))
        {
            GD.Print($"Player {id} already spawned, not spawning again");
            return;
        }
        
        try
        {
            // Get current scene first to verify it's ready
            var currentScene = GetTree().CurrentScene;
            if (currentScene == null)
            {
                GD.PrintErr("Cannot spawn player: current scene is null, will retry");
                // Retry with delay
                var timer = new Timer();
                timer.WaitTime = 0.5f;
                timer.OneShot = true;
                AddChild(timer);
                timer.Timeout += () => {
                    SpawnPlayer(id);
                    timer.QueueFree();
                };
                timer.Start();
                return;
            }
            
            GD.Print($"Current scene is: {currentScene.Name}");
            
            // Instantiate the player
            var player = PlayerScene.Instantiate();
            if (player == null)
            {
                GD.PrintErr($"Failed to instantiate player scene for id {id}");
                return;
            }
            
            player.Name = id.ToString();
            
            // Set network authority
            player.SetMultiplayerAuthority((int)id);
            
            // First add to scene, then set position
            currentScene.AddChild(player);
            _players[id] = player;
            
            // Find a spawn point - need to have at least one in the scene!
            var spawnPoints = GetTree().GetNodesInGroup("SpawnPoints");
            GD.Print($"Found {spawnPoints.Count} spawn points");
            if (spawnPoints.Count > 0)
            {
                var spawnIndex = (int)(GD.Randi() % (uint)spawnPoints.Count);
                var spawnPoint = spawnPoints[spawnIndex] as Node3D;
                var playerNode = player as Node3D;
                
                // Make sure player is inside tree before setting global position
                if (playerNode.IsInsideTree() && spawnPoint.IsInsideTree())
                {
                    playerNode.GlobalPosition = spawnPoint.GlobalPosition;
                    GD.Print($"Player {id} spawned at position {playerNode.GlobalPosition}");
                }
                else
                {
                    // Fallback: set local position
                    playerNode.Position = new Vector3(0, 2, 0);
                    GD.Print($"Player {id} spawned at local position");
                }
            }
            else
            {
                // No spawn points, use a default position
                GD.PrintErr("No spawn points found, using default position");
                (player as Node3D).Position = new Vector3(0, 2, 0); // Slightly above the ground
            }
            
            GD.Print($"Successfully spawned player for peer: {id}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Exception while spawning player: {e.Message}");
        }
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