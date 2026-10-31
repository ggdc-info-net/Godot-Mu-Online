using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Login,
        SelectCharacter,
        InGame
    }

    public GameState CurrentState { get; private set; } = GameState.Login;

    public string PlayerToken { get; set; } = "";
    public Dictionary<string, object> SelectedCharacter { get; set; } = new();

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        
        // ΔΕΝ καλύπτουμε ChangeGameState εδώ μέσα για να μην μπλοκάρει η Godot.
        // Η LoginScene θα ανοίξει αυτόματα αν την ορίσεις ως Main Scene στα Project Settings.
    }

    // Καθαρά void μέθοδος, χωρίς async/await
    public void ChangeGameState(GameState newState)
{
    CurrentState = newState;

    // Καθαρίζουμε αμέσως τον τρέχοντα 3D χάρτη ώστε να μην τρέχουν τα _Process του κατά την αλλαγή σκηνής!
    MapManager.Instance?.ClearExistingMap();

    switch (CurrentState)
    {
        case GameState.Login:
            GetTree().ChangeSceneToFile("res://Scenes/Login Scene/LoginScene.tscn");
            break;
        case GameState.SelectCharacter:
            GetTree().ChangeSceneToFile("res://Scenes/Select Character Scene/SelectCharacterScene.tscn");
            break;
        case GameState.InGame:
            GetTree().ChangeSceneToFile("res://Scenes/Game Scene/GameScene.tscn");
            break;
    }
}
}