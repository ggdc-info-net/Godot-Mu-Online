using Godot;
using Client.Main.Content;

namespace MuGodot.Controls.UI.Login;

public partial class MuLogo : Control
{
    [Export] public float LogoScale { get; set; } = 1.0f; 
    
    [Export] public float OffsetX { get; set; } = 640f;    // ➕ Θετικό = Δεξιά, ➖ Αρνητικό = Αριστερά
    [Export] public float OffsetY { get; set; } = 40f;   // ➕ Θετικό = Πιο κάτω, ➖ Αρνητικό = Πιο ψηλά

    public override async void _Ready()
    {
        Name = "MuLogo";
        SetAnchorsPreset(Control.LayoutPreset.FullRect);

        Texture2D logoTexture = await TextureLoader.Instance.PrepareAndGetTexture("Logo/MU-logo.tga");

        if (logoTexture != null)
        {
            Vector2 nativeSize = logoTexture.GetSize();
            float scaledWidth = nativeSize.X * LogoScale;
            float scaledHeight = nativeSize.Y * LogoScale;

            TextureRect logoRect = new TextureRect
            {
                Texture = logoTexture,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(scaledWidth, scaledHeight)
            };

            // Κεντράρισμα στο πάνω μέρος με προσθήκη των Custom Offsets
            logoRect.AnchorLeft = 0.5f;
            logoRect.AnchorRight = 0.5f;
            logoRect.AnchorTop = 0.0f;
            logoRect.AnchorBottom = 0.0f;

            float halfW = scaledWidth / 2f;
            
            // Προσθέτουμε το OffsetX στο κέντρο και το OffsetY από την κορυφή
            logoRect.OffsetLeft = -halfW + OffsetX;
            logoRect.OffsetRight = halfW + OffsetX;
            logoRect.OffsetTop = OffsetY;
            logoRect.OffsetBottom = OffsetY + scaledHeight;

            AddChild(logoRect);
            GD.Print($"[MuLogo] MU Logo loaded with Offset X: {OffsetX}, Y: {OffsetY}");
        }
        else
        {
            GD.PrintErr("[MuLogo] Failed to load logo texture via TextureLoader!");
        }
    }
}