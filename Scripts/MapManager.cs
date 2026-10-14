using Godot;
using System.Threading.Tasks;
using Client.Main.Core.Utilities; // Για το MapDatabase

public partial class MapManager : Node3D
{
    public static MapManager Instance { get; private set; }

    [Export] public ushort CurrentWorldIndex { get; set; } = 1; // 1 = Lorencia (ή 0 ανάλογα με τα αρχεία σου)

    private TerrainControl _terrainControl;
    private ObjectControl _objectControl;
    private Node _worldConfigInstance;
    private Node3D _playerInstance;

    public override async void _Ready()
    {
        Instance = this;
        await LoadMapAsync(CurrentWorldIndex); 
    }

    public async Task LoadMapAsync(ushort worldIndex)
    {
        CurrentWorldIndex = worldIndex;
        string mapName = MapDatabase.GetMapName(worldIndex);
        
        GD.Print($"[MapManager] Loading Map ID: {worldIndex} ({mapName})...");

        ClearExistingMap();

        // 1. Δημιουργία Configuration Script για τον χάρτη (π.χ. LorenciaWorld)
        LoadWorldConfiguration(worldIndex, mapName);

        // 2. Δημιουργία Terrain
        _terrainControl = new TerrainControl();
        _terrainControl.Name = "TerrainControl";
        _terrainControl.WorldIndex = (short)worldIndex;
        AddChild(_terrainControl);

        // 3. Δημιουργία Objects
        _objectControl = new ObjectControl();
        _objectControl.Name = "ObjectControl";
        _objectControl.WorldIndex = (short)worldIndex;
        AddChild(_objectControl);

        while (_terrainControl == null || !_terrainControl.IsLoaded)
        {
            await ToSignal(GetTree(), "process_frame");
        }

        // 4. Spawn Παίκτη με τις σωστές συντεταγμένες του χάρτη
        SpawnPlayer();

        GD.Print($"[MapManager] Successfully loaded {mapName}!");
    }

    private void LoadWorldConfiguration(ushort worldIndex, string mapName)
    {
        // Ανάλογα με τον χάρτη, φορτώνουμε το αντίστοιχο config script
        if (mapName == "Lorencia")
        {
            _worldConfigInstance = new LorenciaWorld();
            _worldConfigInstance.Name = "LorenciaConfig";
            AddChild(_worldConfigInstance);
        }
        // Αργότερα προσθέτεις εδώ: else if (mapName == "Devias") { ... }
    }

    private void SpawnPlayer()
    {
        var playerScene = GD.Load<PackedScene>("res://Scenes/Player.tscn"); 
        if (playerScene != null)
        {
            _playerInstance = (Node3D)playerScene.Instantiate();
            _playerInstance.Name = "Player";
            AddChild(_playerInstance);

            if (_playerInstance is PlayerControl playerCtrl)
            {
                // Παίρνουμε το DefaultSpawnTile από το LorenciaWorld αν υπάρχει, αλλιώς default
                Vector2I spawnTile = new Vector2I(138, 124);
                if (_worldConfigInstance is LorenciaWorld lorenciaWorld)
                {
                    spawnTile = lorenciaWorld.DefaultSpawnTile;
                }

                playerCtrl.TeleportToTile(spawnTile.X, spawnTile.Y, 45f);
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

        if (_worldConfigInstance != null && GodotObject.IsInstanceValid(_worldConfigInstance))
        {
            _worldConfigInstance.QueueFree();
            _worldConfigInstance = null;
        }

        if (_playerInstance != null && GodotObject.IsInstanceValid(_playerInstance))
        {
            _playerInstance.QueueFree();
            _playerInstance = null;
        }
    }
}