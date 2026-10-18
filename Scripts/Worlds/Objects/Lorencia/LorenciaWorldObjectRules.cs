using Client.Main.Utils;
using Godot;
using MuGodot.Objects.Worlds.Lorencia;

public sealed class LorenciaWorldObjectRules : IWorldObjectRules
{
    public static LorenciaWorldObjectRules Instance { get; } = new();

    private LorenciaWorldObjectRules() { }

    public bool IsSpecialObject(short type)
    {
        // 50, 51: FireLight | 52: Bonfire | 55: DungeonGate | 80: Bridge | 90: StreetLight | 150: Candle
        return type is 50 or 51 or 52 or 55 or 80 or 90 or 133 or 150 or 98 or 122 or 117 or 133 or 119 or 105;
    }


    public void ApplyInstanceRules(short type, Node3D instanceNode)
    {
        if (type == 133)
    {
        // Το διαγράφει εντελώς από τη μνήμη και τη σκηνή μόλις γεννηθεί
        instanceNode.CallDeferred(Node.MethodName.QueueFree);
        return;
    }
    

        if (instanceNode == null)
            return;

        switch (type)
        {
            // Φωτιές με φλόγα + φως
            case 50 or 51 or 52:
                LorenciaFireEmitter.AttachTo(instanceNode, type);
                break;

            // Γέφυρα με 2 φωτιές
            case 80:
                LorenciaFireEmitter.AttachTo(instanceNode, type, variantIndex: 0, nodeName: "LorenciaBridgeFireA");
                LorenciaFireEmitter.AttachTo(instanceNode, type, variantIndex: 1, nodeName: "LorenciaBridgeFireB");
                break;

            // Κερί & Φανάρι δρόμου (Μόνο PointLight)
            case 90:
                LorenciaPointLightEmitter.AttachTo(instanceNode, type);
                LorenciaSurfaceFixer.ApplyTo(instanceNode, type);

                break;

            // Πύλη Dungeon (2 PointLights στις κολώνες)
            case 55:
                LorenciaPointLightEmitter.AttachTo(instanceNode, type, variantIndex: 0, nodeName: "LorenciaDungeonGateLightA");
                LorenciaPointLightEmitter.AttachTo(instanceNode, type, variantIndex: 1, nodeName: "LorenciaDungeonGateLightB");
                break;

            case 150:
                LorenciaPointLightEmitter.AttachTo(instanceNode, type);
                break;

            case 98 or 122 or 117 or 119 or 105:
                LorenciaSurfaceFixer.ApplyTo(instanceNode, type);
                break;
        }
    }
}