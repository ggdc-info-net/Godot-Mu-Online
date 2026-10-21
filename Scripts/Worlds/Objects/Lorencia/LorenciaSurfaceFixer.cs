using Godot;

namespace MuGodot.Objects.Worlds.Lorencia;

public static class LorenciaSurfaceFixer
{
    public static void ApplyTo(Node3D instanceNode, short sourceType)
    {
        if (instanceNode == null || !GodotObject.IsInstanceValid(instanceNode))
            return;

        MeshInstance3D meshInst = FindMeshInstance(instanceNode);
        if (meshInst == null || meshInst.Mesh == null)
            return;

        switch (sourceType)
        {
            case 98 or 90 or 117 or 119 or 105 or 150: 
                FixCarriageSurfaces(meshInst);
                break;
        }
    }

    private static void FixCarriageSurfaces(MeshInstance3D meshInst)
    {
        int surfaceCount = meshInst.Mesh.GetSurfaceCount();

        for (int i = 0; i < surfaceCount; i++)
        {
            var mat = meshInst.Mesh.SurfaceGetMaterial(i) as StandardMaterial3D;
            if (mat == null) continue;

            string texName = mat.AlbedoTexture?.ResourceName ?? "UnknownTexture";

            // Εκτύπωση στην κονσόλα για να δούμε ποιο surface είναι η σκιά/φως της άμαξας
            GD.Print($"[Carriage Type 98] Surface {i}: Texture={texName}, Transparency={mat.Transparency}, Blend={mat.BlendMode}");

            // Δοκιμαστικά εφαρμόζουμε Additive Blend στο τελευταίο surface της άμαξας
            if (i == surfaceCount - 1)
            {
                var customMat = (StandardMaterial3D)mat.Duplicate();

                customMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                customMat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
                customMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                customMat.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;

                meshInst.SetSurfaceOverrideMaterial(i, customMat);
            }
        }
    }

    public static MeshInstance3D FindMeshInstance(Node3D root)
    {
        if (root is MeshInstance3D directMesh)
            return directMesh;

        return root.GetNodeOrNull<MeshInstance3D>("Skeleton3D/Mesh")
            ?? root.GetNodeOrNull<MeshInstance3D>("Mesh");
    }
}