using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ServerModels;

public partial class PlayFabServer : Node
{
    [Export] public string TitleId { get; set; } = "1AE973";
    [Export] public string SecretKey { get; set; } = "XH77UE4J7TWFXEN6Z7UDM35CN6FW5MCN68RTUA99J4UKQMPOUU";
    
    private bool _isInitialized = false;
    
    [Signal]
    public delegate void ServerInitializedEventHandler();
    
    [Signal]
    public delegate void ServerInitFailedEventHandler(string error);
    
    public override void _Ready()
    {
        // Set title and secret key from exported properties or environment variables
        PlayFabSettings.staticSettings.TitleId = TitleId;
        PlayFabSettings.staticSettings.DeveloperSecretKey = SecretKey;
        
        if (System.Environment.GetEnvironmentVariable("TITLE_ID") != null)
        {
            TitleId = System.Environment.GetEnvironmentVariable("TITLE_ID");
            PlayFabSettings.staticSettings.TitleId = TitleId;
        }
        
        if (System.Environment.GetEnvironmentVariable("PLAYFAB_SECRET_KEY") != null)
        {
            SecretKey = System.Environment.GetEnvironmentVariable("PLAYFAB_SECRET_KEY");
            PlayFabSettings.staticSettings.DeveloperSecretKey = SecretKey;
        }
        
        // Here you would normally initialize the multiplayer server using the GSDK.
        // For this example, we simply mark the server as initialized.
        _isInitialized = true;
        EmitSignal(SignalName.ServerInitialized);
    }
    
    public bool IsInitialized()
    {
        return _isInitialized;
    }
    
    public async void UpdatePlayerStatistics(string playFabId, string statName, int value)
    {
        if (!_isInitialized)
        {
            GD.PrintErr("Cannot update stats - server not initialized");
            return;
        }
        
        var request = new UpdatePlayerStatisticsRequest
        {
            PlayFabId = playFabId,
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
            // Use the async version of the API call.
            var result = await PlayFabServerAPI.UpdatePlayerStatisticsAsync(request);
            GD.Print($"Successfully updated statistic {statName} for player {playFabId}");
        }
        catch(Exception ex)
        {
            GD.PrintErr($"Error updating statistics: {ex.Message}");
        }
    }
    
    public async void ReportMatch(Dictionary<string, int> playerScores)
    {
        if (!_isInitialized || playerScores.Count == 0)
        {
            return;
        }
        
        foreach (var entry in playerScores)
        {
            var playFabId = entry.Key;
            var score = entry.Value;
            
            var request = new WriteServerPlayerEventRequest
            {
                PlayFabId = playFabId,
                EventName = "match_completed",
                Body = new Dictionary<string, object>
                {
                    { "Score", score }
                }
            };
            
            try
            {
                // Call the async method to record the event.
                var result = await PlayFabServerAPI.WritePlayerEventAsync(request);
                GD.Print($"Recorded match result for player {playFabId}: {score} points");
            }
            catch(Exception ex)
            {
                GD.PrintErr($"Error recording match result: {ex.Message}");
            }
        }
    }
}
