using Godot;

public partial class LorenciaWorld : Node
{
    [Export] public string WorldName { get; set; } = "Lorencia";
    [Export] public string BackgroundMusicPath { get; set; } = @"F:\Program Files\MuCloneData\Data\Music\MuTheme.mp3"; // .mp3 αρχείο
    [Export] public string AmbientSoundPath { get; set; } = @"F:\Program Files\MuCloneData\Data\Sound\aBird1.wav";    // .wav αρχείο
    [Export] public Vector2I DefaultSpawnTile { get; set; } = new Vector2I(138, 124);

    private AudioStreamPlayer _musicPlayer;
    private AudioStreamPlayer _ambientPlayer;

    private double _ambientTimer = 0.0;
    private double _nextAmbientInterval = 15.0;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        _musicPlayer = new AudioStreamPlayer { Name = "MusicPlayer" };
        _ambientPlayer = new AudioStreamPlayer { Name = "AmbientPlayer" };
        AddChild(_musicPlayer);
        AddChild(_ambientPlayer);

        // 1. Φόρτωση και συνεχές loop για το MP3 της μουσικής
        PlayExternalMP3(_musicPlayer, BackgroundMusicPath);

        // 2. Προετοιμασία για το τυχαίο .wav ambient
        PrepareAmbient(_ambientPlayer, AmbientSoundPath);
        ResetAmbientTimer();
    }

    public override void _Process(double delta)
    {
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
            mp3Stream.Loop = true; // Το MP3 υποστηρίζει εγγενές looping στη Godot!

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
}