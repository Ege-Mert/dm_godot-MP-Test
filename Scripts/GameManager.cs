using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Node
{
    [Export] public NodePath NetworkManagerPath { get; set; }
    [Export] public NodePath ScoreboardUIPath { get; set; }
    [Export] public int MatchDurationSeconds { get; set; } = 300; // 5 minutes
    
    private NetworkManager _networkManager;
    private Control _scoreboardUI;
    private Label _timerLabel;
    private double _matchTimeRemaining;
    private bool _matchActive = false;
    
    [Signal]
    public delegate void MatchStartedEventHandler();
    
    [Signal]
    public delegate void MatchEndedEventHandler();
    
    public override void _Ready()
    {
        GD.Print("GameManager: _Ready called");
        GD.Print($"Current scene: {GetTree().CurrentScene?.Name}, My ID: {Multiplayer.GetUniqueId()}");
        
        if (NetworkManagerPath.IsEmpty)
        {
            // Try to find the autoloaded NetworkManager
            _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        }
        else
        {
            _networkManager = GetNode<NetworkManager>(NetworkManagerPath);
        }
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkManager not found! Game will not function properly.");
            return;
        }
        
        // Always verify NetworkManager is initialized correctly
        GD.Print($"GameManager successfully connected to NetworkManager. IsHost: {_networkManager.IsHost()}");
        
        if (!ScoreboardUIPath.IsEmpty)
        {
            _scoreboardUI = GetNode<Control>(ScoreboardUIPath);
            if (_scoreboardUI != null)
            {
                _timerLabel = _scoreboardUI.GetNode<Label>("%TimerLabel");
                _scoreboardUI.Visible = false;
                
                // Set up the scoreboard title
                var titleLabel = _scoreboardUI.GetNode<Label>("%Title");
                if (titleLabel != null)
                {
                    titleLabel.Text = "SCOREBOARD";
                }
            }
        }
        
        // Connect signals from NetworkManager
        _networkManager.PeerConnected += OnPlayerConnected;
        _networkManager.PeerDisconnected += OnPlayerDisconnected;
        
        // Only the server starts the match
        if (_networkManager.IsHost())
        {
            // Use CallDeferred to ensure the scene is fully loaded
            CallDeferred(nameof(StartMatch));
        }
        
        // Update debug info with network ID
        var networkStatus = GetNode<Label>("../CanvasLayer/GameHUD/NetworkStatus");
        if (networkStatus != null)
        {
            networkStatus.Text = $"Network ID: {Multiplayer.GetUniqueId()}"; 
        }
    }
    
    public override void _Process(double delta)
    {
        if (_matchActive && _networkManager.IsHost())
        {
            _matchTimeRemaining -= delta;
            
            if (_matchTimeRemaining <= 0)
            {
                _matchTimeRemaining = 0;
                EndMatch();
            }
            
            // Update the timer displays on all clients (every second to reduce network traffic)
            if (Mathf.FloorToInt((float)_matchTimeRemaining) != Mathf.FloorToInt((float)(_matchTimeRemaining + delta)))
            {
                Rpc(nameof(UpdateMatchTimer), _matchTimeRemaining);
            }
        }
        
        // Show/hide scoreboard on Tab key
        if (Input.IsActionJustPressed("ui_focus_next") && _scoreboardUI != null) // Tab key
        {
            _scoreboardUI.Visible = true;
            UpdateScoreboard();
        }
        else if (Input.IsActionJustReleased("ui_focus_next") && _scoreboardUI != null)
        {
            _scoreboardUI.Visible = false;
        }
        
        // Add a debug toggle display that can show current player positions for testing
        if (Input.IsKeyPressed(Key.F1))
        {
            DebugShowPlayerPositions();
        }
    }
    
    private void DebugShowPlayerPositions()
    {
        // Find debug info label
        var debugLabel = GetNode<Label>("../CanvasLayer/DebugInfo/Label");
        if (debugLabel == null) return;
        
        // Get all players and show their positions
        var players = _networkManager.GetPlayers();
        string info = $"My ID: {Multiplayer.GetUniqueId()}\n";
        info += $"Total players: {players.Count}\n";
        
        foreach (var player in players)
        {
            if (player.Value is Node3D node3D)
            {
                info += $"Player {player.Key}: {node3D.Position}\n";
            }
        }
        
        debugLabel.Text = info;
    }
    
    private void StartMatch()
    {
        _matchTimeRemaining = MatchDurationSeconds;
        _matchActive = true;
        
        GD.Print("Match started!");
        EmitSignal(SignalName.MatchStarted);
        
        // Ensure server spawns its own player immediately
        if (_networkManager.IsHost())
        {
            GD.Print("Server is spawning its own player (ID 1)...");
            // Force spawn of server player
            _networkManager.SpawnPlayer(1);
        }
    }
    
    private void EndMatch()
    {
        _matchActive = false;
        
        GD.Print("Match ended!");
        EmitSignal(SignalName.MatchEnded);
        
        // Determine winner
        var winner = DetermineWinner();
        
        // Show match results
        Rpc(nameof(ShowMatchResults), winner);
    }
    
    private string DetermineWinner()
    {
        var players = _networkManager.GetPlayers();
        long winnerId = 0;
        int highestKills = -1;
        
        foreach (var player in players)
        {
            if (player.Value is NetworkedPlayer networkPlayer)
            {
                if (networkPlayer.Kills > highestKills)
                {
                    highestKills = networkPlayer.Kills;
                    winnerId = player.Key;
                }
            }
        }
        
        return winnerId > 0 ? $"Player {winnerId}" : "No winner";
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void UpdateMatchTimer(double timeRemaining)
    {
        if (_timerLabel != null)
        {
            var minutes = (int)(timeRemaining / 60);
            var seconds = (int)(timeRemaining % 60);
            _timerLabel.Text = $"Time: {minutes:00}:{seconds:00}";
        }
    }
    
    private void UpdateScoreboard()
    {
        if (_scoreboardUI == null) return;
        
        var playersList = _scoreboardUI.GetNode<VBoxContainer>("%PlayersList");
        if (playersList == null) return;
        
        // Clear existing entries
        foreach (var child in playersList.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add header
        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "Player", CustomMinimumSize = new Vector2(100, 0) });
        header.AddChild(new Label { Text = "Kills", CustomMinimumSize = new Vector2(50, 0) });
        header.AddChild(new Label { Text = "Deaths", CustomMinimumSize = new Vector2(50, 0) });
        playersList.AddChild(header);
        
        // Get all players and their stats
        var players = _networkManager.GetPlayers();
        GD.Print($"Updating scoreboard with {players.Count} players");
        foreach (var player in players)
        {
            if (player.Value is NetworkedPlayer networkPlayer)
            {
                var playerRow = new HBoxContainer();
                playerRow.AddChild(new Label { Text = $"Player {networkPlayer.PlayerId}", CustomMinimumSize = new Vector2(100, 0) });
                playerRow.AddChild(new Label { Text = networkPlayer.Kills.ToString(), CustomMinimumSize = new Vector2(50, 0) });
                playerRow.AddChild(new Label { Text = networkPlayer.Deaths.ToString(), CustomMinimumSize = new Vector2(50, 0) });
                playersList.AddChild(playerRow);
            }
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void ShowMatchResults(string winner)
    {
        // Create a popup with match results
        var popup = new AcceptDialog();
        popup.DialogText = $"Match ended! Winner: {winner}";
        popup.Title = "Match Results";
        
        // Add a button to return to main menu
        popup.AddButton("Return to Main Menu", true);
        
        popup.Confirmed += () => ReturnToMainMenu();
        
        // In Godot 4, instead of Custom_Action, connect to custom buttons directly
        // The button we added will trigger the Confirmed signal
        
        AddChild(popup);
        popup.PopupCentered();
    }
    
    private void ReturnToMainMenu()
    {
        _networkManager.Disconnect();
    }
    
    private void OnPlayerConnected(long id)
    {
        GD.Print($"GameManager: Player {id} connected");
        GD.Print($"Current peers: {string.Join(", ", Multiplayer.GetPeers())}");
        
        // If match is active, sync the match state with the new player
        if (_matchActive && _networkManager.IsHost())
        {
            RpcId((int)id, nameof(UpdateMatchTimer), _matchTimeRemaining);
        }
        
        // Update scoreboard for everyone
        if (_networkManager.IsHost())
        {
            CallDeferred(nameof(UpdateScoreboard));
        }
    }
    
    private void OnPlayerDisconnected(long id)
    {
        GD.Print($"GameManager: Player {id} disconnected");
        UpdateScoreboard();
    }
}