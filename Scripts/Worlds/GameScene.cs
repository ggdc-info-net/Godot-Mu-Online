using Godot;

public partial class GameScene : Node3D
{
    public override async void _Ready()
    {
        GD.Print("[GameScene] Initializing Lorencia and Spawning Player...");

        // Δίνουμε λίγο χρόνο να αρχικοποιηθεί πλήρως το δέντρο της σκηνής
        await ToSignal(GetTree(), "process_frame");

        if (MapManager.Instance != null)
        {
            // Φορτώνουμε τον χάρτη 1 (Lorencia) με ενεργοποιημένο το spawn του παίκτη
            await MapManager.Instance.LoadMapAsync(1, spawnPlayer: true);
            GD.Print("[GameScene] Lorencia loaded successfully!");
        }
        else
        {
            GD.PrintErr("[GameScene] ERROR: MapManager.Instance is NULL!");
        }
    }
}