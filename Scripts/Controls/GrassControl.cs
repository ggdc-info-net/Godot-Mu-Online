using Godot;
using MuGodot;
using System.Threading.Tasks;

public partial class GrassControl : Node3D
{
    [Export] public short WorldIndex { get; set; }

    [ExportGroup("Grass Density")]
    [Export(PropertyHint.Range, "0.1,2.0,0.05")] public float DensityScale { get; set; } = 0.85f;
    [Export] public int MaxInstances { get; set; } = 16000;

    [ExportGroup("Blade Size")]
    [Export(PropertyHint.Range, "0.3,2.5,0.05")] public float BladeWidthScale { get; set; } = 0.85f;
    [Export(PropertyHint.Range, "0.3,2.5,0.05")] public float BladeHeightScale { get; set; } = 0.80f;
    [Export(PropertyHint.Range, "1.0,2.0,0.05")] public float MaxRandomScale { get; set; } = 1.30f;

    [ExportGroup("Wind & Visuals")]
    [Export(PropertyHint.Range, "0.1,5.0,0.05")] public float WindSpeed { get; set; } = 1.35f;
    [Export(PropertyHint.Range, "0.0,3.0,0.05")] public float WindStrength { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0.5,4.0,0.1")] public float GrassBrightness { get; set; } = 1.85f;

    private MuGrassRenderer _grassRenderer;
    private Camera3D _camera;
    private Node3D _player;

    public override async void _Ready()
    {
        var terrainControl = GetParent().GetNodeOrNull<TerrainControl>("TerrainControl");
        if (terrainControl == null)
        {
            GD.PrintErr("[GrassControl] TerrainControl not found!");
            return;
        }

        int waitFrames = 0;
        while (_player == null || _player.GlobalPosition == Vector3.Zero)
        {
            _player = GetParent().GetNodeOrNull<Node3D>("Player");
            await ToSignal(GetTree(), "process_frame");
            waitFrames++;
            if (waitFrames > 60) break;
        }

        _camera = GetViewport().GetCamera3D();
        Vector3 initialPos = GetTargetPosition();

        float adaptedBrightness = WorldIndex switch
        {
            3 => 0.90f, // Devias (Χιόνι - πιο σκοτεινό/ψυχρό για να μημπιφτείνει λευκό)
            4 => 1.05f, // Noria (Δάσος)
            _ => 1.15f  // Lorencia
        };

        _grassRenderer = new MuGrassRenderer(terrainControl)
        {
            MaxInstances = MaxInstances,
            DensityScale = DensityScale,
            BladeWidthScale = BladeWidthScale,
            BladeHeightScale = BladeHeightScale,
            MaxRandomScale = MaxRandomScale,
            WindSpeed = WindSpeed,
            WindStrength = WindStrength,
            RebuildCameraMoveThreshold = 150f,
            ProgressiveRebuild = true,
            RebuildTilesPerFrame = 250
        };

        _grassRenderer.ConfigureGrass(adaptedBrightness, 0);

        GD.Print($"[GrassControl] Building grass around: {initialPos}");
        await _grassRenderer.BuildAsync(WorldIndex, this, initialPos);
    }

    public override void _Process(double delta)
    {
        if (_grassRenderer == null)
            return;

        // Επιτρέπει live αλλαγές στον άνεμο την ώρα που τρέχει το παιχνίδι
        _grassRenderer.WindSpeed = WindSpeed;
        _grassRenderer.WindStrength = WindStrength;

        if (_player == null || !GodotObject.IsInstanceValid(_player))
            _player = GetParent().GetNodeOrNull<Node3D>("Player");

        _grassRenderer.Update(delta, GetTargetPosition());
    }

    private Vector3 GetTargetPosition()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player) && _player.GlobalPosition != Vector3.Zero)
            return _player.GlobalPosition;

        if (_camera == null || !GodotObject.IsInstanceValid(_camera))
            _camera = GetViewport().GetCamera3D();

        if (_camera != null)
            return _camera.GlobalPosition;

        return new Vector3(13800f, 0f, -12400f);
    }

    public void Clear()
    {
        if (_grassRenderer != null)
        {
            _grassRenderer.Clear();
            _grassRenderer = null;
        }
        QueueFree();
    }
}