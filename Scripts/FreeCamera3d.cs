using Godot;

public partial class FreeCamera3d : Camera3D
{
    [Export] public float MoveSpeed = 10f;
    [Export] public float MouseSensitivity = 0.1f;
    [Export] public float SprintMultiplier = 3f;

    private float yaw;
    private float pitch;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion)
        {
            yaw -= mouseMotion.Relative.X * MouseSensitivity;
            pitch -= mouseMotion.Relative.Y * MouseSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            RotationDegrees = new Vector3(pitch, yaw, 0);
        }
    }

    public override void _Process(double delta)
    {
        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("move_forward"))
            direction -= Transform.Basis.Z;
        if (Input.IsActionPressed("move_backward"))
            direction += Transform.Basis.Z;
        if (Input.IsActionPressed("move_left"))
            direction -= Transform.Basis.X;
        if (Input.IsActionPressed("move_right"))
            direction += Transform.Basis.X;
        if (Input.IsActionPressed("move_up"))
            direction += Transform.Basis.Y;
        if (Input.IsActionPressed("move_down"))
            direction -= Transform.Basis.Y;

        float speed = MoveSpeed;

        if (Input.IsActionPressed("move_fast")) // bind this to Shift
        speed *= SprintMultiplier;

        Position += direction.Normalized() * MoveSpeed * (float)delta;
    }
}
