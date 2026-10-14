using Godot;
using Client.Main.Content;

public sealed class LorenciaWorldObjectRules : IWorldObjectRules
{
    public static LorenciaWorldObjectRules Instance { get; } = new();

    // 1. ΜΟΝΑΔΙΚΑ instances στη μνήμη για ΟΛΕΣ τις φωτιές του χάρτη!
    private static readonly Shader FireShader = GD.Load<Shader>("res://Shaders/Lorencia/firehik01.gdshader");
    private static readonly ShaderMaterial SharedFlameMaterial = new() { Shader = FireShader };
    private static readonly QuadMesh SharedFlameQuad = new() { Size = new Vector2(0.75f, 0.9f) };
    
    private static bool _isTextureLoadingOrLoaded = false;

    public bool IsSpecialObject(short type)
    {
        return type == 52;
    }

    public void ApplyInstanceRules(short type, Node3D instanceNode)
    {
        if (type == 52)
        {
            // Φορτώνουμε την εικόνα ΜΟΝΟ μία φορά για όλο το παιχνίδι!
            if (!_isTextureLoadingOrLoaded)
            {
                _isTextureLoadingOrLoaded = true;
                LoadFireTextureOnceAsync("Effect/firehik01.jpg");
            }

            // 1. Το φως
            var fireLight = new OmniLight3D
            {
                Name = "CampfireLight",
                LightColor = new Color(1.0f, 0.6f, 0.2f),
                LightEnergy = 3.5f,
                OmniRange = 5.0f,
                LightSpecular = 0.0f,
                Position = new Vector3(0, 0.6f, 0)
            };
            instanceNode.AddChild(fireLight);

            // 2. Ο ΔΙΟΡΘΩΤΗΣ ΑΞΟΝΩΝ
            var pivot = new Node3D { Name = "UprightPivot" };
            instanceNode.AddChild(pivot);
            pivot.RotationDegrees = new Vector3(90, 0, 0);

            // 3. Το επίπεδο - Δίνουμε το Shared Mesh και το Shared Material!
            var flameMesh = new MeshInstance3D
            {
                Name = "RealFlame",
                Mesh = SharedFlameQuad,
                MaterialOverride = SharedFlameMaterial,
                Position = new Vector3(0, 0.8f, 0)
            };
            pivot.AddChild(flameMesh); 

            // 4. ANIMATION με μικρή τυχαιότητα στον χρόνο (ώστε να μην φουντώνουν ταυτόχρονα)
            float t1 = (float)GD.RandRange(0.35, 0.45);
            float t2 = (float)GD.RandRange(0.25, 0.35);
            float t3 = (float)GD.RandRange(0.30, 0.40);

            var tween = instanceNode.CreateTween().SetLoops(); 
            tween.TweenProperty(flameMesh, "scale", new Vector3(1.05f, 1.15f, 1.05f), t1);
            tween.Parallel().TweenProperty(fireLight, "light_energy", 4.2f, t1);
            
            tween.Chain().TweenProperty(flameMesh, "scale", new Vector3(0.95f, 0.90f, 0.95f), t2);
            tween.Parallel().TweenProperty(fireLight, "light_energy", 2.8f, t2);
            
            tween.Chain().TweenProperty(flameMesh, "scale", new Vector3(1.0f, 1.0f, 1.0f), t3);
            tween.Parallel().TweenProperty(fireLight, "light_energy", 3.5f, t3); 
        }
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
            _isTextureLoadingOrLoaded = false; // Αν αποτύχει, επιτρέπουμε να ξαναπροσπαθήσει
        }
    }
}