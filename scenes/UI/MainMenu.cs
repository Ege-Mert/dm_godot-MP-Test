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
        
        // Connect button signals
        _hostButton.Pressed += OnHostButtonPressed;
        _joinButton.Pressed += OnJoinButtonPressed;
        _quitButton.Pressed += OnQuitButtonPressed;
        
        // Use CallDeferred to find NetworkManager after the scene is fully loaded
        CallDeferred(nameof(FindNetworkManager));
    }
    
    private void FindNetworkManager()
    {
        // Get the autoloaded NetworkManager
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        
        if (_networkManager == null)
        {
            GD.PrintErr("NetworkManager autoload not found! Verify it's configured in the project settings.");
            ShowErrorDialog("NetworkManager not found. Cannot connect to multiplayer.\nPlease make sure 'NetworkManager' is set as an autoload in Project Settings.");
            SetButtonsEnabled(false); // Disable buttons since we can't connect
            return;
        }
        
        // Connect NetworkManager signals
        ConnectNetworkManagerSignals();
        GD.Print("Successfully connected to NetworkManager autoload");
    }
    
    private void ConnectNetworkManagerSignals()
    {
        _networkManager.NetworkError += OnNetworkError;
        _networkManager.ServerStarted += OnServerStarted;
        _networkManager.ClientConnected += OnClientConnected;
    }
    
    private void OnHostButtonPressed()
    {
        GD.Print("Starting server...");
        
        // Check if NetworkManager is available
        if (_networkManager == null)
        {
            GD.Print("Trying to find NetworkManager again...");
            FindNetworkManager();
            
            if (_networkManager == null)
            {
                ShowErrorDialog("Cannot start server: NetworkManager not found.");
                return;
            }
        }
        
        // Disable buttons to prevent multiple clicks
        SetButtonsEnabled(false);
        
        _networkManager.StartServer();
    }
    
    private void OnJoinButtonPressed()
    {
        GD.Print("Joining server...");
        
        // Check if NetworkManager is available
        if (_networkManager == null)
        {
            GD.Print("Trying to find NetworkManager again...");
            FindNetworkManager();
            
            if (_networkManager == null)
            {
                ShowErrorDialog("Cannot join server: NetworkManager not found.");
                return;
            }
        }
        
        // Disable buttons to prevent multiple clicks
        SetButtonsEnabled(false);
        
        // Show IP input dialog
        var dialog = new AcceptDialog();
        var container = new VBoxContainer();
        var ipInput = new LineEdit { Text = _networkManager.ServerIP };
        var ipLabel = new Label { Text = "Server IP:" };
        
        container.AddChild(ipLabel);
        container.AddChild(ipInput);
        dialog.DialogText = "";
        dialog.AddChild(container);
        
        dialog.Confirmed += () => {
            _networkManager.ServerIP = ipInput.Text;
            _networkManager.JoinServer();
        };
        dialog.Canceled += () => SetButtonsEnabled(true);
        
        AddChild(dialog);
        dialog.PopupCentered();
    }
    
    private void SetButtonsEnabled(bool enabled)
    {
        _hostButton.Disabled = !enabled;
        _joinButton.Disabled = !enabled;
        _quitButton.Disabled = !enabled;
    }
    
    private void OnNetworkError(string error)
    {
        GD.Print("Network error: " + error);
        ShowErrorDialog(error);
        SetButtonsEnabled(true);
    }
    
    private void OnServerStarted()
    {
        GD.Print("Server started successfully!");
    }
    
    private void OnClientConnected()
    {
        GD.Print("Connected to server successfully!");
    }
    
    private void ShowErrorDialog(string message)
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Error";
        dialog.DialogText = message;
        AddChild(dialog);
        dialog.PopupCentered();
    }
    
    private void OnQuitButtonPressed()
    {
        GetTree().Quit();
    }
}