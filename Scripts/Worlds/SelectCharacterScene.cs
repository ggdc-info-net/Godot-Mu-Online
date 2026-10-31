using Godot;
using MuGodot;
using System.Threading.Tasks;
using Client.Main.Controls.UI.Login; // Αν χρειάζεται για το ServerButton

public partial class SelectCharacterScene : Node3D
{
    private CameraControl _selectcharacterCamera;

    public override async void _Ready()
{
    GD.Print("[SelectCharacterScene] Initializing SelectCharacter scene...");

    int timeout = 50;
    while (MapManager.Instance == null && timeout > 0)
    {
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        timeout--;
    }

    if (MapManager.Instance == null)
    {
        GD.PrintErr("[SelectCharacterScene] MapManager instance not found!");
        return;
    }

    // 1. ΔΗΜΙΟΥΡΓΟΥΜΕ ΚΑΙ ΡΥΘΜΙΖΟΥΜΕ ΤΗΝ ΚΑΜΕΡΑ ΠΡΩΤΑ ΑΠ' ΟΛΑ
    _selectcharacterCamera = new CameraControl { Name = "SelectCharacterCamera" };
    AddChild(_selectcharacterCamera);
    _selectcharacterCamera.Initialize(() => Vector3.Zero);

    Vector3 targetPos = new Vector3(16186.9f, 604.4f, -12056.8f);
    Vector3 targetRotDeg = new Vector3(-5.5f, 82.4f, 0.0f);

    _selectcharacterCamera.GlobalPosition = targetPos;
    _selectcharacterCamera.GlobalRotationDegrees = targetRotDeg;

    if (_selectcharacterCamera.Camera != null)
    {
        _selectcharacterCamera.Camera.Fov = 16.3f;
        _selectcharacterCamera.Camera.Near = 10.0f;
        _selectcharacterCamera.Camera.Far = 15000.0f;
        _selectcharacterCamera.Camera.Current = true;
        GD.Print("[SelectCharacterScene] Camera successfully initialized first!");
    }

    // 2. ΤΩΡΑ ΦΟΡΤΩΝΟΥΜΕ ΤΟΝ ΧΑΡΤΗ (ώστε τα αντικείμενα να βρουν την κάμερα έτοιμη)
    await MapManager.Instance.LoadMapAsync(94, spawnPlayer: false);

    // 3. ΠΕΡΙΜΕΝΟΥΜΕ 2 FRAMES να γίνουν render τα meshes του terrain
    await ToSignal(GetTree(), "process_frame");
    await ToSignal(GetTree(), "process_frame");

    // 4. ΑΣΦΑΛΗΣ ΣΥΝΔΕΣΗ ΜΕ ΤΟ OBJECT CONTROL
    var objCtrl = ObjectControl.Instance;
    if (objCtrl != null && GodotObject.IsInstanceValid(objCtrl))
    {
        objCtrl.SetPlayerPositionSource(() => _selectcharacterCamera.GlobalPosition);
        
        var cullingSys = objCtrl.GetNodeOrNull<ObjectCullingSystem>("../ObjectCullingSystem");
        if (cullingSys != null && GodotObject.IsInstanceValid(cullingSys) && _selectcharacterCamera.Camera != null)
        {
            cullingSys.Initialize(_selectcharacterCamera.Camera);
            cullingSys.EnableDistanceFogAndObjectCulling = false; 
        }
    }
}

    public override void _Process(double delta)
    {
        
    }
}