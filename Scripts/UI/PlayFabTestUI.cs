using Godot;
using System;

public partial class PlayFabTestUI : Control
{
    [Export] public NodePath PlayFabManagerPath { get; set; }
    
    private PlayFabManager _playFabManager;
    private LineEdit _customIdInput;
    private Button _loginButton;
    private Button _updateStatsButton;
    private Label _statusLabel;
    
    public override void _Ready()
    {
        // Get UI components
        _customIdInput = GetNode<LineEdit>("%CustomIdInput");
        _loginButton = GetNode<Button>("%LoginButton");
        _updateStatsButton = GetNode<Button>("%UpdateStatsButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        
        // Get PlayFabManager
        _playFabManager = GetNode<PlayFabManager>(PlayFabManagerPath);
        
        if (_playFabManager == null)
        {
            GD.PrintErr("PlayFabManager not found at path: " + PlayFabManagerPath);
            UpdateStatus("Error: PlayFabManager not found!");
            return;
        }
        
        // Connect signals
        _loginButton.Pressed += OnLoginButtonPressed;
        _updateStatsButton.Pressed += OnUpdateStatsButtonPressed;
        
        // Connect PlayFab signals
        _playFabManager.LoginSuccess += OnLoginSuccess;
        _playFabManager.LoginFailed += OnLoginFailed;
        
        // Initial UI setup
        _updateStatsButton.Disabled = true;
        UpdateStatus("Ready to test PlayFab connection");
    }
    
    private void OnLoginButtonPressed()
    {
        string customId = _customIdInput.Text.Trim();
        if (string.IsNullOrEmpty(customId))
        {
            customId = "TestPlayer" + new Random().Next(1000, 9999);
            _customIdInput.Text = customId;
        }
        
        UpdateStatus($"Logging in with ID: {customId}...");
        _loginButton.Disabled = true;
        _playFabManager.LoginWithCustomId(customId);
    }
    
    private void OnUpdateStatsButtonPressed()
    {
        var score = new Random().Next(100, 1000);
        UpdateStatus($"Updating player score to {score}...");
        _playFabManager.UpdatePlayerStatistics("Score", score);
    }
    
    private void OnLoginSuccess(string playFabId)
    {
        UpdateStatus($"Login successful! PlayFab ID: {playFabId}");
        _loginButton.Disabled = false;
        _updateStatsButton.Disabled = false;
    }
    
    private void OnLoginFailed(string error)
    {
        UpdateStatus($"Login failed: {error}");
        _loginButton.Disabled = false;
        _updateStatsButton.Disabled = true;
    }
    
    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
        GD.Print($"PlayFab Status: {message}");
    }
}