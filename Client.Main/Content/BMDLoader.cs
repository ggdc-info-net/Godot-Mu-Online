using Godot;

namespace Client.Main.Content
{
    public partial class BMDLoader : Node
{
    public static BMDLoader Instance;

    public override void _Ready()
    {
        Instance = this;
        GD.Print("BMDLoader READY");
    }
}
}

