using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;

public partial class PlayFabManager : Node
{
    [Export] public string TitleId { get; set; } = "1AE973";
    
    private string _playFabId;
    private bool _isLoggedIn = false;
    
    [Signal]
    public delegate void LoginSuccessEventHandler(string playFabId);
    
    [Signal]
    public delegate void LoginFailedEventHandler(string error);
    
    [Signal]
    public delegate void ServerRequestSuccessEventHandler(string serverIp, int serverPort);
    
    [Signal]
    public delegate void ServerRequestFailedEventHandler(string error);
    
    public override void _Ready()
    {
        // Set the TitleId from your exported property
        PlayFabSettings.staticSettings.TitleId = TitleId;
    }
    
    public async void LoginWithCustomId(string customId, bool createAccount = true)
    {
        GD.Print($"Attempting to login with custom ID: {customId}");
        
        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = createAccount
        };
        
        try 
        {
            // Call the async login method.
            var loginResult = await PlayFabClientAPI.LoginWithCustomIDAsync(request);
            OnLoginSuccess(loginResult.Result);
        }
        catch(Exception ex)
        {
            // Wrap the exception in a PlayFabError for consistency.
            OnLoginError(new PlayFabError { ErrorMessage = ex.Message });
        }
    }
    
    private void OnLoginSuccess(LoginResult result)
    {
        _playFabId = result.PlayFabId;
        _isLoggedIn = true;
        GD.Print($"PlayFab login success. PlayFabId: {_playFabId}");
        EmitSignal(SignalName.LoginSuccess, _playFabId);
    }
    
    private void OnLoginError(PlayFabError error)
    {
        GD.PrintErr($"PlayFab login error: {error.ErrorMessage}");
        EmitSignal(SignalName.LoginFailed, error.ErrorMessage);
    }
    
    public bool IsLoggedIn()
    {
        return _isLoggedIn;
    }
    
    public string GetPlayFabId()
    {
        return _playFabId;
    }
    
    public void RequestDedicatedServer(string buildId, string region = "EastUs")
    {
        if (!_isLoggedIn)
        {
            GD.PrintErr("Cannot request server - not logged in");
            EmitSignal(SignalName.ServerRequestFailed, "Not logged in to PlayFab");
            return;
        }
        
        GD.Print($"Requesting multiplayer server with buildId {buildId} in region {region}");
        // For testing purposes we simulate a server allocation.
        CallDeferred("_OnServerAllocated", "127.0.0.1", 28960);
    }
    
    private void _OnServerAllocated(string ip, int port)
    {
        GD.Print($"PlayFab server allocated: {ip}:{port}");
        EmitSignal(SignalName.ServerRequestSuccess, ip, port);
    }
    
    public async void UpdatePlayerStatistics(string statName, int value)
    {
        if (!_isLoggedIn)
        {
            GD.PrintErr("Cannot update stats - not logged in");
            return;
        }
        
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = statName,
                    Value = value
                }
            }
        };
        
        try 
        {
            var result = await PlayFabClientAPI.UpdatePlayerStatisticsAsync(request);
            GD.Print($"Successfully updated statistic {statName}");
        }
        catch(Exception ex)
        {
            GD.PrintErr($"Error updating statistics: {ex.Message}");
        }
    }
}
