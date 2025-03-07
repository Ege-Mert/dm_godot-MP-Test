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
    
    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        
        if (PlayerScene == null)
        {
            GD.PrintErr("PlayerScene not set in NetworkManager");
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
    
    public void SpawnPlayer(long id)
    {
        if (PlayerScene == null)
        {
            GD.PrintErr("Player scene not set in NetworkManager");
            return;
        }
        
        // Don't spawn if player already exists
        if (_players.ContainsKey(id) && _players[id] != null && IsInstanceValid(_players[id]))
        {
            GD.Print($"Player {id} already spawned, not spawning again");
            return;
        }
        
        var player = PlayerScene.Instantiate();
        player.Name = id.ToString();
        
        // Set network authority
        player.SetMultiplayerAuthority((int)id);
        
        // Find a spawn point
        var spawnPoints = GetTree().GetNodesInGroup("SpawnPoints");
        if (spawnPoints.Count > 0)
        {
            var spawnIndex = GD.Randi() % (uint)spawnPoints.Count;
            (player as Node3D).GlobalPosition = (spawnPoints[(int)spawnIndex] as Node3D).GlobalPosition;
        }
        
        // Add to the scene
        GetTree().CurrentScene.AddChild(player);
        _players[id] = player;
        
        GD.Print($"Spawned player for peer: {id}");
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