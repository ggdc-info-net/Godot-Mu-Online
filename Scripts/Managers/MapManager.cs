using Godot;
using System.Threading.Tasks;
using Client.Main.Core.Utilities;
using MuGodot; // Για το MapDatabase

public partial class MapManager : Node3D
{
    public static MapManager Instance { get; private set; }

    [Export] public ushort CurrentWorldIndex { get; set; } = 1; // 1 = Lorencia

    private TerrainControl _terrainControl;
    private ObjectControl _objectControl;
    private Node _worldConfigInstance;
    private Node3D _playerInstance;
    private MuGrassRenderer _grassRenderer;
    private GrassControl _grassControl;

    private bool _isLoading = false;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
    }

    public async Task LoadMapAsync(ushort worldIndex, bool spawnPlayer = true)
    {
        if (_isLoading)
        {
            GD.Print("[MapManager] Already loading a map, ignoring duplicate request.");
            return;
        }
        _isLoading = true;

        try
        {
            CurrentWorldIndex = worldIndex;
            string mapName = MapDatabase.GetMapName(worldIndex);
            
            GD.Print($"[MapManager] Loading Map ID: {worldIndex} ({mapName})...");

            // Καθαρίζουμε τον προηγούμενο χάρτη (π.χ. World 95 πριν φορτώσει το World 94)
            ClearExistingMap();

            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;

            // 1. Δημιουργία Configuration Script για τον χάρτη (αν υπάρχει)
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

            // 📌 ΠΕΡΙΜΕΝΟΥΜΕ ΝΑ ΦΟΡΤΩΣΕΙ ΠΛΗΡΩΣ ΤΟ TERRAIN
            while (_terrainControl != null && GodotObject.IsInstanceValid(_terrainControl) && !_terrainControl.IsLoaded)
            {
                await ToSignal(GetTree(), "process_frame");
            }

            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;

            // 4. Δημιουργία GrassControl (αφού το Terrain είναι πλέον 100% έτοιμο)
            _grassControl = new GrassControl();
            _grassControl.Name = "GrassControl";
            _grassControl.WorldIndex = (short)worldIndex;
            AddChild(_grassControl);

            // 5. Spawn Παίκτη (μόνο όταν είμαστε InGame, όχι σε Login / SelectCharacter)
            if (spawnPlayer)
            {
                SpawnPlayer();
            }
            else
            {
                GD.Print("[MapManager] Skipping player spawn for this scene.");
            }

            GD.Print($"[MapManager] Successfully loaded {mapName}!");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadWorldConfiguration(ushort worldIndex, string mapName)
    {
        // Φορτώνουμε ειδικά configs ανάλογα με τον χάρτη
        if (mapName == "Lorencia")
        {
            _worldConfigInstance = new LorenciaWorld();
            _worldConfigInstance.Name = "LorenciaConfig";
            AddChild(_worldConfigInstance);
        }
        // Προσθέτεις εδώ μελλοντικά configs (π.χ. NoriaWorld, DeviasWorld)
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
                Vector2I spawnTile = new Vector2I(138, 124);
                if (_worldConfigInstance is LorenciaWorld lorenciaWorld)
                {
                    spawnTile = lorenciaWorld.DefaultSpawnTile;
                }

                playerCtrl.TeleportToTile(spawnTile.X, spawnTile.Y, 45f);
            }
        }
        else
        {
            GD.PrintErr("[MapManager] Player.tscn not found at res://Scenes/Player.tscn!");
        }
    }
    
    public void ClearExistingMap()
    {
        // RemoveChild πριν το QueueFree ώστε να απελευθερώνονται αμέσως τα ονόματα των Nodes!
        if (_grassControl != null && GodotObject.IsInstanceValid(_grassControl))
        {
            _grassControl.Clear();
            RemoveChild(_grassControl);
            _grassControl.QueueFree();
            _grassControl = null;
        }

        if (_objectControl != null && GodotObject.IsInstanceValid(_objectControl))
        {
            // Καθαρίζουμε και τα βοηθητικά συστήματα που προσθέτει το ObjectControl στον γονέα του
            var cullingSys = GetNodeOrNull<Node>("ObjectCullingSystem");
            if (cullingSys != null)
            {
                RemoveChild(cullingSys);
                cullingSys.QueueFree();
            }

            var occlusionSys = GetNodeOrNull<Node>("LorenciaHouseOcclusionSystem");
            if (occlusionSys != null)
            {
                RemoveChild(occlusionSys);
                occlusionSys.QueueFree();
            }

            RemoveChild(_objectControl);
            _objectControl.QueueFree();
            _objectControl = null;
        }

        if (_terrainControl != null && GodotObject.IsInstanceValid(_terrainControl))
        {
            RemoveChild(_terrainControl);
            _terrainControl.QueueFree();
            _terrainControl = null;
        }

        if (_worldConfigInstance != null && GodotObject.IsInstanceValid(_worldConfigInstance))
        {
            RemoveChild(_worldConfigInstance);
            _worldConfigInstance.QueueFree();
            _worldConfigInstance = null;
        }

        if (_playerInstance != null && GodotObject.IsInstanceValid(_playerInstance))
        {
            RemoveChild(_playerInstance);
            _playerInstance.QueueFree();
            _playerInstance = null;
        }
    }
}