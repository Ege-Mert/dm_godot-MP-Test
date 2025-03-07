using Godot;
using System;

public partial class MainMenu : Control
{
    [Export] public NodePath NetworkManagerPath { get; set; }
    
    private Button _hostButton;
    private Button _joinButton;
    private Button _quitButton;
    private NetworkManager _networkManager;
    
    public override void _Ready()
    {
        _hostButton = GetNode<Button>("%HostButton");
        _joinButton = GetNode<Button>("%JoinButton");
        _quitButton = GetNode<Button>("%QuitButton");
        
        _networkManager = GetNode<NetworkManager>(NetworkManagerPath);
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkManager not found at path: " + NetworkManagerPath);
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