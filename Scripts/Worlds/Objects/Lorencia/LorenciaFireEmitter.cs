using Godot;
using Client.Main.Content;

namespace MuGodot.Objects.Worlds.Lorencia;

public sealed partial class LorenciaFireEmitter : Node3D
{
    // 1. Τα ΔΙΚΑ ΣΟΥ Shared resources που δουλεύουν 100%
    private static readonly Shader FireShader = GD.Load<Shader>("res://Shaders/Lorencia/firehik01.gdshader");
    private static readonly ShaderMaterial SharedFlameMaterial = new() { Shader = FireShader };
    private static readonly QuadMesh SharedFlameQuad = new() { Size = new Vector2(0.75f, 0.9f) };

    private static bool _isTextureLoadingOrLoaded = false;

    [Export] public short SourceType { get; set; }
    [Export] public int VariantIndex { get; set; }

    public static void AttachTo(Node3D instanceNode, short sourceType, int variantIndex = 0, string nodeName = null)
    {
        if (instanceNode == null || !GodotObject.IsInstanceValid(instanceNode))
            return;

        string resolvedNodeName = nodeName 
            ?? (variantIndex == 0 ? "LorenciaFireEmitter" : $"LorenciaFireEmitter_{variantIndex}");

        if (instanceNode.GetNodeOrNull<LorenciaFireEmitter>(resolvedNodeName) != null)
            return;

        var emitter = new LorenciaFireEmitter
        {
            Name = resolvedNodeName,
            SourceType = sourceType,
            VariantIndex = variantIndex
        };

        instanceNode.AddChild(emitter);
    }

    public override void _Ready()
    {
        if (!_isTextureLoadingOrLoaded)
        {
            _isTextureLoadingOrLoaded = true;
            LoadFireTextureOnceAsync("Effect/firehik01.jpg");
        }

        // Παίρνουμε την ακριβή τοπική θέση και κλίμακα για το συγκεκριμένο αντικείμενο
        var (anchorPos, flameScale) = GetFireSettings(SourceType, VariantIndex);

        // 1. Ο ΔΙΟΡΘΩΤΗΣ ΑΞΟΝΩΝ (Ακριβώς όπως στον κώδικά σου)
        var pivot = new Node3D 
        { 
            Name = "UprightPivot",
            Position = anchorPos,
            RotationDegrees = new Vector3(90, 0, 0)
        };
        AddChild(pivot);

        // 2. Το φως
        var fireLight = new OmniLight3D
        {
            Name = "CampfireLight",
            LightColor = new Color(0.886f, 0.345f, 0.133f),
            LightEnergy = 150f,
            OmniRange = 150.0f,
            LightSpecular = 0.0f,
            Position = new Vector3(0, 0.6f, 0),
            RotationDegrees = new Vector3(90, 0, 0)
        };
        pivot.AddChild(fireLight);

        // 3. Η Φλόγα (με το σωστό scale ανάλογα με το αντικείμενο)
        var flameMesh = new MeshInstance3D
        {
            Name = "RealFlame",
            Mesh = SharedFlameQuad,
            MaterialOverride = SharedFlameMaterial,
            Position = new Vector3(0, 0.8f, 0),
            Scale = new Vector3(flameScale, flameScale, flameScale),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        pivot.AddChild(flameMesh);

        // 4. Το Tween Animation
        float t1 = (float)GD.RandRange(0.35, 0.45);
        float t2 = (float)GD.RandRange(0.25, 0.35);
        float t3 = (float)GD.RandRange(0.30, 0.40);

        Vector3 s1 = new Vector3(1.05f, 1.15f, 1.05f) * flameScale;
        Vector3 s2 = new Vector3(0.95f, 0.90f, 0.95f) * flameScale;
        Vector3 s3 = Vector3.One * flameScale;

        // var tween = CreateTween().SetLoops();
        // tween.TweenProperty(flameMesh, "scale", s1, t1);
        // tween.Parallel().TweenProperty(fireLight, "light_energy", 4.2f, t1);

        // tween.Chain().TweenProperty(flameMesh, "scale", s2, t2);
        // tween.Parallel().TweenProperty(fireLight, "light_energy", 2.8f, t2);

        // tween.Chain().TweenProperty(flameMesh, "scale", s3, t3);
        // tween.Parallel().TweenProperty(fireLight, "light_energy", 3.5f, t3);
    }

    /// <summary>
    /// Εδώ ορίζουμε τη θέση του pivot και το μέγεθος της φλόγας για κάθε τύπο αντικειμένου.
    /// (Στο σύστημα του ObjectControl: X = Δεξιά/Αριστερά, Y = Μπροστά/Πίσω, Z = Ύψος)
    /// </summary>
    private static (Vector3 position, float scale) GetFireSettings(short type, int variantIndex)
    {
        return type switch
        {
            // 52: Bonfire01 (Η φωτιά στο έδαφος) -> Θέση (0,0,0), Κανονικό μέγεθος (1.0)
            52 => (new Vector3(0f, 0f, -0.1f), 1.0f),

            // 50: FireLight01 (Ψηλός πυρσός)
            50 => (new Vector3(0f, 0f, 0.95f), 0.5f),

            // 51: FireLight02 (Δεύτερος τύπος πυρσού)
            51 => (new Vector3(0f, -0.3f, -0.15f), 0.3f),

            // 80: Bridge01 (Οι 2 φωτιές της γέφυρας)
            80 => variantIndex == 0
                ? (new Vector3(1.0f, 2.05f, -0.5f), 0.9f)  // Φωτιά Α
                : (new Vector3(1.0f, -2.15f, -0.5f), 0.9f), // Φωτιά Β

            150 => (new Vector3(0f, 0f, 0.3f), 0.2f),
            
            _ => (Vector3.Zero, 1.0f)
        };
    }

    private static async void LoadFireTextureOnceAsync(string texturePath)
    {
        try
        {
            var tex = await TextureLoader.Instance.PrepareAndGetTexture(texturePath);
            if (tex != null)
            {
                SharedFlameMaterial.SetShaderParameter("albedo_tex", tex);
            }
        }
        catch
        {
            _isTextureLoadingOrLoaded = false;
        }
    }
}