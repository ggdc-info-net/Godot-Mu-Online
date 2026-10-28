using Client.Data;
using Godot;
using MuGodot.Objects.Worlds.Lorencia;

public partial class LorenciaWorld : Node3D
{
    [Export] public string WorldName { get; set; } = "Lorencia";
    [Export] public string BackgroundMusicPath { get; set; } = @"F:\Program Files\MuCloneData\Data\Music\MuTheme.mp3";
    [Export] public string AmbientSoundPath { get; set; } = @"F:\Program Files\MuCloneData\Data\Sound\aBird1.wav";
    [Export] public Vector2I DefaultSpawnTile { get; set; } = new Vector2I(138, 124);

    private AudioStreamPlayer _musicPlayer;
    private AudioStreamPlayer _ambientPlayer;

    private double _ambientTimer = 0.0;
    private double _nextAmbientInterval = 15.0;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    private LorenciaAmbientManager _ambientManager;
    private Camera3D _camera;

    public override async void _Ready()
    {
        GD.Print("[LorenciaWorld] Ready called! Initializing Lorencia world...");

        _musicPlayer = new AudioStreamPlayer { Name = "MusicPlayer" };
        _ambientPlayer = new AudioStreamPlayer { Name = "AmbientPlayer" };
        AddChild(_musicPlayer);
        AddChild(_ambientPlayer);

        PlayExternalMP3(_musicPlayer, BackgroundMusicPath);
        PrepareAmbient(_ambientPlayer, AmbientSoundPath);
        ResetAmbientTimer();

        // Περιμένουμε μέχρι να φορτώσει και να γίνει διαθέσιμο το TerrainControl.Instance
        int timeout = 50; // Ασφάλεια για να μην κολλήσει σε infinite loop
        while (TerrainControl.Instance == null && timeout > 0)
        {
            await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
            timeout--;
        }

        if (TerrainControl.Instance == null)
        {
            GD.PrintErr("[LorenciaWorld] ERROR: TerrainControl.Instance timed out and is still NULL!");
            return;
        }

        GD.Print("[LorenciaWorld] TerrainControl.Instance found successfully.");

        // Δημιουργία και Αρχικοποίηση του Ambient Manager
        _ambientManager = new LorenciaAmbientManager();
        _ambientManager.Name = "LorenciaAmbientManager";
        AddChild(_ambientManager);

        _ambientManager.Initialize(TerrainControl.Instance);

        await _ambientManager.ConfigureForWorldAsync(1, this, limitEffects: false);
        GD.Print("[LorenciaWorld] AmbientManager configuration completed.");

        ApplyWorldAtmosphere();
    }

    public override void _Process(double delta)
    {
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera3D();
            if (_camera != null)
            {
                GD.Print($"[LorenciaWorld] Camera found: {_camera.Name}");
            }
        }

        if (_camera != null && _ambientManager != null)
        {
            _ambientManager.Update(delta, _camera.GlobalPosition);
        }

        // --------------------------------------------------
        // Υπάρχον Audio Logic για το Ambient Sound
        // --------------------------------------------------
        if (_ambientPlayer == null || _ambientPlayer.Stream == null) return;

        if (!_ambientPlayer.Playing)
        {
            _ambientTimer -= delta;
            if (_ambientTimer <= 0)
            {
                _ambientPlayer.Play();
                GD.Print("[Audio] Playing periodic ambient wind sound...");
                ResetAmbientTimer();
            }
        }
    }

    private void PlayExternalMP3(AudioStreamPlayer player, string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            GD.PrintErr($"[Audio Error] MP3 file not found: {filePath}");
            return;
        }

        try
        {
            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            var mp3Stream = new AudioStreamMP3();
            mp3Stream.Data = fileBytes;
            mp3Stream.Loop = true;

            player.Stream = mp3Stream;
            player.Play();
            GD.Print($"[Audio] Successfully loaded and playing MP3: {filePath}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Audio Error] Failed to load MP3: {ex.Message}");
        }
    }

    private void PrepareAmbient(AudioStreamPlayer player, string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;

        var wavStream = AudioStreamWav.LoadFromFile(filePath);
        if (wavStream != null)
        {
            player.Stream = wavStream;
        }
    }

    private void ResetAmbientTimer()
    {
        _nextAmbientInterval = _rng.RandfRange(10.0f, 25.0f);
        _ambientTimer = _nextAmbientInterval;
    }

    private void ApplyWorldAtmosphere()
{
    var worldEnv = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
    if (worldEnv == null)
    {
        worldEnv = new WorldEnvironment { Name = "WorldEnvironment" };
        AddChild(worldEnv);
    }

    if (worldEnv.Environment == null)
    {
        worldEnv.Environment = new Godot.Environment();
    }

    var env = worldEnv.Environment;

    // 1. Ξεκινάμε την ομίχλη σίγουρα ΠΙΣΩ από τον παίκτη!
    // Η κάμερα έχει MaxDistance = 1800f * WorldToGodot. 
    // Άρα στα 2400f ο παίκτης είναι 100% έξω από την ομίχλη ακόμα και στο τέρμα zoom out.
    float fogBegin = Constants.WorldToGodot * 2400f; 
    float fogEnd   = Constants.WorldToGodot * 9500f;

    Color lorenciaFogColor = new Color(0.15f, 0.14f, 0.11f, 1.0f);

    env.BackgroundMode = Godot.Environment.BGMode.Color;
    env.BackgroundColor = lorenciaFogColor;

    // 📌 2. ΤΟ ΜΥΣΤΙΚΟ: Κλειδώνουμε το Ambient Light και τις αντανακλάσεις σε καθαρό ΛΕΥΚΟ!
    // Έτσι το BackgroundColor (η ομίχλη) ΔΕΝ βάφει ποτέ τον παίκτη και τα μοντέλα!
    env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
    env.AmbientLightColor = Colors.White;
    env.AmbientLightSkyContribution = 0.0f; // 0% επιρροή από το χρώμα του φόντου/ουρανού!
    env.AmbientLightEnergy = 0.65f;         // Καθαρός, ουδέτερος φωτισμός
    env.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled; // Κόβει τις χρωματιστές γυαλάδες πάνω στις πανοπλίες

    // 3. Ρυθμίσεις Ομίχλης
    env.VolumetricFogEnabled = false;
    env.FogEnabled = true;
    env.FogMode = Godot.Environment.FogModeEnum.Depth;
    
    env.FogLightColor = lorenciaFogColor;
    env.FogLightEnergy = 1.0f;
    env.FogDensity = 1.0f;
    env.FogDepthBegin = fogBegin;
    env.FogDepthEnd = fogEnd;
    env.FogDepthCurve = 1.8f; // Λίγο πιο υψηλή καμπύλη ώστε κοντά στο fogBegin η ομίχλη να είναι σχεδόν 0%
    env.FogSkyAffect = 1.0f;
}
}