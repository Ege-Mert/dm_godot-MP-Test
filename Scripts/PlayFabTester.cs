using Godot;
using System;

public partial class PlayFabTester : Control
{
    [Export] public PlayFabManager PlayFabManager { get; set; }
    [Export] public Label StatusLabel { get; set; }
    
    private LineEdit _customIdInput;
    private Button _loginButton;
    private Button _updateStatsButton;
    
    public override void _Ready()
    {
        _customIdInput = GetNode<LineEdit>("VBoxContainer/CustomIdInput");
        _loginButton = GetNode<Button>("VBoxContainer/LoginButton");
        _updateStatsButton = GetNode<Button>("VBoxContainer/UpdateStatsButton");
        
        if (PlayFabManager == null)
        {
            GD.PrintErr("PlayFabManager not assigned in PlayFabTester");
            return;
        }
        
        // Set up signal connections
        PlayFabManager.LoginSuccess += OnLoginSuccess;
        PlayFabManager.LoginFailed += OnLoginFailed;
        
        // Connect button signals
        _loginButton.Pressed += OnLoginButtonPressed;
        _updateStatsButton.Pressed += OnUpdateStatsButtonPressed;
        
        // Initially disable the update stats button
        _updateStatsButton.Disabled = true;
        
        UpdateStatus("Ready to test PlayFab connection");
    }
    
    private void OnLoginButtonPressed()
    {
        string customId = _customIdInput.Text.Trim();
        if (string.IsNullOrEmpty(customId))
        {
            customId = "TestPlayer" + Guid.NewGuid().ToString().Substring(0, 8);
            _customIdInput.Text = customId;
        }
        
        UpdateStatus($"Attempting to login with ID: {customId}...");
        PlayFabManager.LoginWithCustomId(customId);
    }
    
    private void OnUpdateStatsButtonPressed()
    {
        if (!PlayFabManager.IsLoggedIn())
        {
            UpdateStatus("Cannot update stats - not logged in");
            return;
        }
        
        UpdateStatus("Updating player statistics...");
        PlayFabManager.UpdatePlayerStatistics("TestScore", new Random().Next(100, 1000));
    }
    
    private void OnLoginSuccess(string playFabId)
    {
        UpdateStatus($"Login successful! PlayFab ID: {playFabId}");
        _updateStatsButton.Disabled = false;
    }
    
    private void OnLoginFailed(string error)
    {
        UpdateStatus($"Login failed: {error}");
        _updateStatsButton.Disabled = true;
    }
    
    private void UpdateStatus(string message)
    {
        if (StatusLabel != null)
        {
            StatusLabel.Text = message;
        }
        GD.Print($"PlayFab Status: {message}");
    }
}