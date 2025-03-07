using Godot;
using System;

public partial class MainMenu : Control
{
    private Button _hostButton;
    private Button _joinButton;
    private Button _quitButton;
    private NetworkManager _networkManager;
    
    public override void _Ready()
    {
        _hostButton = GetNode<Button>("%HostButton");
        _joinButton = GetNode<Button>("%JoinButton");
        _quitButton = GetNode<Button>("%QuitButton");
        
        // Get the NetworkManager from AutoLoad
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkManager AutoLoad not found! Make sure to add it in Project Settings > AutoLoad");
            return;
        }
        
        _hostButton.Pressed += OnHostButtonPressed;
        _joinButton.Pressed += OnJoinButtonPressed;
        _quitButton.Pressed += OnQuitButtonPressed;
    }
    
    private void OnHostButtonPressed()
    {
        GD.Print("Starting server...");
        _networkManager.StartServer();
    }
    
    private void OnJoinButtonPressed()
    {
        GD.Print("Joining server...");
        _networkManager.JoinServer();
    }
    
    private void OnQuitButtonPressed()
    {
        GetTree().Quit();
    }
}