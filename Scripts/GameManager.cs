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
        _networkManager = GetNode<NetworkManager>(NetworkManagerPath);
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkManager not found at path: " + NetworkManagerPath);
            return;
        }
        
        if (!ScoreboardUIPath.IsEmpty)
        {
            _scoreboardUI = GetNode<Control>(ScoreboardUIPath);
            _timerLabel = _scoreboardUI.GetNode<Label>("%TimerLabel");
        }
        
        // Only the server starts the match
        if (_networkManager.IsHost())
        {
            StartMatch();
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
            
            // Update the timer displays on all clients
            Rpc(nameof(UpdateMatchTimer), _matchTimeRemaining);
        }
        
        // Show/hide scoreboard on Tab key
        if (Input.IsActionJustPressed("ui_focus_next") && _scoreboardUI != null) // Tab key
        {
            _scoreboardUI.Visible = true;
        }
        else if (Input.IsActionJustReleased("ui_focus_next") && _scoreboardUI != null)
        {
            _scoreboardUI.Visible = false;
        }
    }
    
    private void StartMatch()
    {
        _matchTimeRemaining = MatchDurationSeconds;
        _matchActive = true;
        
        GD.Print("Match started!");
        EmitSignal(SignalName.MatchStarted);
        
        // Spawn player for server
        _networkManager.SpawnPlayer(1);
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
            _timerLabel.Text = $"{minutes:00}:{seconds:00}";
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
}