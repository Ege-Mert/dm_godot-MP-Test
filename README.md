# Multiplayer FPS Deathmatch in Godot with C# and PlayFab

A simple multiplayer first-person shooter deathmatch game built with Godot 4 using C# and PlayFab for dedicated server hosting.

## Project Structure

- `Scripts/`: C# scripts for game logic
  - `PlayerController.cs`: FPS controller with network synchronization
  - `NetworkManager.cs`: Handles client/server network communication
  - `GameManager.cs`: Manages game state and UI
  - `Weapon.cs`: Basic weapon implementation with raycasting
  - `PlayFab/`: PlayFab integration scripts
    - `PlayFabManager.cs`: Client-side PlayFab integration
    - `PlayFabServer.cs`: Server-side PlayFab integration

## Development Setup

1. Install Godot 4.x .NET (C#) version
2. Open the project in Godot
3. Configure Input mappings (Project > Project Settings > Input Map):
   - `ui_left`, `ui_right`, `ui_up`, `ui_down` (Movement)
   - `ui_accept` (Jump)
   - `sprint` (Sprint)
   - `fire` (Weapon firing)
   - `freefly` (Toggle noclip mode)

## PlayFab Setup

1. Create a PlayFab account and title at [PlayFab.com](https://playfab.com)
2. Update the TitleId in PlayFabManager.cs
3. Follow the instructions for setting up multiplayer dedicated servers in PlayFab

## Running the Game

- Use F5 in Godot editor to launch the game
- Host a game by clicking "Host Game"
- Join a game by clicking "Join Game" 

## Dedicated Server Setup

See the documentation for detailed instructions on deploying dedicated servers to PlayFab.
