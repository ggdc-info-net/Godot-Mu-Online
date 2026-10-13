using Godot;
using Client.Main.Content;

public sealed class LorenciaWorldObjectRules : IWorldObjectRules
{
    public static LorenciaWorldObjectRules Instance { get; } = new();

    public bool IsSpecialObject(short type)
    {
        return type == 52;
    }

    public void ApplyInstanceRules(short type, Node3D instanceNode)
    {
        if (type == 52)
        {
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

            // 3. Το επίπεδο (QuadMesh)
            var flameMesh = new MeshInstance3D
            {
                Name = "RealFlame",
                Mesh = new QuadMesh { Size = new Vector2(1.2f, 1.5f) },
                Position = new Vector3(0, 0.8f, 0)
            };

            // 4. Το Υλικό (Ξεκινάει ως συμπαγές πορτοκαλί για σιγουριά)
            var flameMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(2.0f, 0.5f, 0.0f), 
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Disabled, // Αρχικά κλειστό
                BillboardKeepScale = true, 
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, 
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };

            flameMesh.MaterialOverride = flameMat;
            pivot.AddChild(flameMesh); 

            // 5. Φορτώνουμε την εικόνα στο παρασκήνιο (Fire-and-forget)
            // Προσοχή: Εδώ βάζουμε το path του MU (Effect/...)
            LoadFireTextureAsync(flameMat, "Effect/firehik01.jpg"); 

            // ===============================================================
            // 6. ANIMATION: Κάνουμε τη φωτιά να "χορεύει" (Flicker Effect)
            // ===============================================================
            var tween = instanceNode.CreateTween().SetLoops(); // SetLoops = Επαναλαμβάνεται για πάντα!
            
            // Φάση 1: Η φωτιά φουντώνει (διαρκεί 0.4 δευτερόλεπτα)
            tween.TweenProperty(flameMesh, "scale", new Vector3(1.05f, 1.15f, 1.05f), 0.4f);
            tween.Parallel().TweenProperty(fireLight, "light_energy", 4.2f, 0.4f);
            
            // Φάση 2: Η φωτιά χαμηλώνει (διαρκεί 0.3 δευτερόλεπτα)
            tween.Chain().TweenProperty(flameMesh, "scale", new Vector3(0.95f, 0.90f, 0.95f), 0.3f);
            tween.Parallel().TweenProperty(fireLight, "light_energy", 2.8f, 0.3f);
            
            // Φάση 3: Επιστρέφει στο κανονικό (διαρκεί 0.35 δευτερόλεπτα)
            tween.Chain().TweenProperty(flameMesh, "scale", new Vector3(1.0f, 1.0f, 1.0f), 0.35f);
            tween.Parallel().TweenProperty(fireLight, "light_energy", 3.5f, 0.35f); 
        }
    }

    // Αυτή η μέθοδος τρέχει ασύγχρονα, ΧΩΡΙΣ να μπλοκάρει τη δημιουργία της φωτιάς!
    private async void LoadFireTextureAsync(StandardMaterial3D mat, string texturePath)
    {
        try
        {
            // Καλούμε τον δικό σου TextureLoader
            var tex = await TextureLoader.Instance.PrepareAndGetTexture(texturePath);

            if (tex != null)
            {
                // Μόλις βρει την εικόνα:
                mat.AlbedoTexture = tex; // Του βάζει την εικόνα
                mat.AlbedoColor = new Color(1.5f, 1.5f, 1.5f); // Το κάνει λευκό για να μη χαλάσει τα χρώματα της φωτιάς

                // Ανοίγουμε τη διαφάνεια και τη "λάμψη" (Additive Blend)
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
            }
        }
        catch 
        { 
            // Αν κάτι πάει στραβά, θα μείνει το πορτοκαλί τετράγωνο και θα ξέρουμε ότι απέτυχε η φόρτωση.
        }
    }
}