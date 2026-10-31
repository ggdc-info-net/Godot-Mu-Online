using Godot;

public partial class DebugOverlay : CanvasLayer
{
    private Camera3D _camera;
    private double _printTimer = 0.0;

    public override void _Process(double delta)
    {
        if (_camera == null || !GodotObject.IsInstanceValid(_camera))
        {
            _camera = GetViewport().GetCamera3D();
            if (_camera == null) return;
        }

        _printTimer += delta;
        
        // Εκτύπωση στο Output κάθε 0.5 δευτερόλεπτα (throttle)
        if (_printTimer >= 0.5)
        {
            _printTimer = 0.0;

            Vector3 pos = _camera.GlobalPosition;
            Vector3 rot = _camera.GlobalRotationDegrees;
            float near = _camera.Near;
            float far = _camera.Far;
            float fov = _camera.Fov;

            GD.Print($"[Camera Info] " +
                     $"Pos(X:{pos.X:F1}, Y:{pos.Y:F1}, Z:{pos.Z:F1}) | " +
                     $"Rot(Pitch:{rot.X:F1}°, Yaw:{rot.Y:F1}°, Roll:{rot.Z:F1}°) | " +
                     $"Near:{near:F1}, Far:{far:F1}, FOV:{fov:F1}°");
        }
    }
}