using Godot;
using MuGodot;
using System.Threading.Tasks;

public partial class LoginScene : Node3D
{
    private CameraControl _loginCamera;

    public override async void _Ready()
{
if (ObjectControl.Instance != null)
{
    ObjectControl.Instance.SetPlayerPositionSource(() => _loginCamera.GlobalPosition);
    
    // 2. Ξανα-αρχικοποιούμε το CullingSystem με τη σωστή κάμερα της LoginScene
    var cullingSys = ObjectControl.Instance.GetNodeOrNull<ObjectCullingSystem>("../ObjectCullingSystem");
    if (cullingSys != null && _loginCamera.Camera != null)
    {
        cullingSys.Initialize(_loginCamera.Camera);
        // Προσωρινά απενεργοποιούμε το culling στη LoginScene για να σιγουρευτούμε ότι φαίνονται όλα
        cullingSys.EnableDistanceFogAndObjectCulling = false; 
    }
}

    GD.Print("[LoginScene] Initializing login scene camera...");

    int timeout = 50;
    while (MapManager.Instance == null && timeout > 0)
    {
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        timeout--;
    }

    if (MapManager.Instance == null)
    {
        GD.PrintErr("[LoginScene] MapManager instance not found!");
        return;
    }

    // Φορτώνουμε τον χάρτη
    await MapManager.Instance.LoadMapAsync(95, spawnPlayer: false);

    // 📌 ΠΕΡΙΜΕΝΟΥΜΕ 2 FRAMES να γίνουν render τα meshes του terrain
    await ToSignal(GetTree(), "process_frame");
    await ToSignal(GetTree(), "process_frame");

    // 1. Δημιουργούμε την κάμερα
    _loginCamera = new CameraControl { Name = "LoginCamera" };
    AddChild(_loginCamera);

    // 2. Αρχικοποιούμε την κάμερα ώστε να δημιουργηθεί εσωτερικά το Camera3D component
    _loginCamera.Initialize(() => Vector3.Zero);

    // 3. Ορίζουμε τις επιθυμητές τιμές
    Vector3 targetPos = new Vector3(24327.1f, 1780.5f, -7539.0f);
    Vector3 targetRotDeg = new Vector3(-9.0f, 40.5f, 0.0f);

    _loginCamera.GlobalPosition = targetPos;
    _loginCamera.GlobalRotationDegrees = targetRotDeg;

    // 4. Ρυθμίσεις Camera3D
    if (_loginCamera.Camera != null)
    {
        _loginCamera.Camera.Fov = 30.0f;
        _loginCamera.Camera.Near = 0.1f;
        _loginCamera.Camera.Far = 15000.0f;
        _loginCamera.Camera.Current = true;
        GD.Print("[LoginScene] Camera successfully initialized and set as Current!");
    }
    else
    {
        GD.PrintErr("[LoginScene] Internal Camera3D is NULL!");
    }
}

    public override void _Process(double delta)
    {
        
    }
}