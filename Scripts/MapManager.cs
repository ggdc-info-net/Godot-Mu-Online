using Godot;
using System.Threading.Tasks;

public partial class MapManager : Node3D
{
    public static MapManager Instance { get; private set; }

    [Export] public short CurrentWorldIndex { get; set; } = 0;

    private TerrainControl _terrainControl;
    private ObjectControl _objectControl;
    private Node3D _playerInstance;

    public override async void _Ready()
    {
        Instance = this;
        await LoadMapAsync(CurrentWorldIndex); 
    }

    public async Task LoadMapAsync(short worldIndex)
    {
        CurrentWorldIndex = worldIndex;
        GD.Print($"[MapManager] Loading Map ID: {worldIndex}...");

        ClearExistingMap();

        _terrainControl = new TerrainControl();
        _terrainControl.Name = "TerrainControl";
        _terrainControl.WorldIndex = worldIndex;
        AddChild(_terrainControl);

        _objectControl = new ObjectControl();
        _objectControl.Name = "ObjectControl";
        _objectControl.WorldIndex = worldIndex;
        AddChild(_objectControl);

        while (_terrainControl == null || !_terrainControl.IsLoaded)
        {
            await ToSignal(GetTree(), "process_frame");
        }

        SpawnPlayer();

        GD.Print($"[MapManager] Map {worldIndex} loaded successfully!");
    }

    private void SpawnPlayer()
    {
        // Φορτώνουμε τη σκηνή του παίκτη δυναμικά
        var playerScene = GD.Load<PackedScene>("res://Scenes/Player.tscn"); 
        if (playerScene != null)
        {
            _playerInstance = (Node3D)playerScene.Instantiate();
            _playerInstance.Name = "Player";
            AddChild(_playerInstance);

            // Αν ο παίκτης έχει μέθοδο TeleportToTile, τον βάζουμε σε αρχικές συντεταγμένες
            if (_playerInstance is PlayerControl playerCtrl)
            {
                playerCtrl.TeleportToTile(138, 138, 45f);
            }
        }
    }
    
    private void ClearExistingMap()
    {
        if (_terrainControl != null && GodotObject.IsInstanceValid(_terrainControl))
        {
            _terrainControl.QueueFree();
            _terrainControl = null;
        }

        if (_objectControl != null && GodotObject.IsInstanceValid(_objectControl))
        {
            _objectControl.QueueFree();
            _objectControl = null;
        }

        if (_playerInstance != null && GodotObject.IsInstanceValid(_playerInstance))
        {
            _playerInstance.QueueFree();
            _playerInstance = null;
        }
    }
}