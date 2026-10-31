using Godot;
using MuGodot;
using System.Threading.Tasks;

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

        // 1. ΔΗΜΙΟΥΡΓΙΑ ΚΑΙ ΡΥΘΜΙΣΗ ΚΑΜΕΡΑΣ
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
        }

        // 2. ΦΟΡΤΩΣΗ ΧΑΡΤΗ 94
        await MapManager.Instance.LoadMapAsync(94, spawnPlayer: false);

        // 3. ΑΝΑΜΟΝΗ 2 FRAMES
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");

        // 4. ΣΥΝΔΕΣΗ ΜΕ OBJECT CONTROL
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

        // 5. ΠΡΟΣΘΗΚΗ ΤΟΥ UI MANAGER ΩΣ CHILD NODE
        var uiManager = new SelectCharacterUIManager();
        AddChild(uiManager);
        GD.Print("[SelectCharacterScene] SelectCharacterUIManager attached successfully.");
    }
}