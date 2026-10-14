using Godot;
using MuGodot.Objects.Worlds;

public static class WorldObjectRulesResolver
{
    public static IWorldObjectRules Resolve(int worldIndex)
    {
        return worldIndex switch
        {
            1 => LorenciaWorldObjectRules.Instance,
            // 2 => DeviasWorldObjectRules.Instance, (μελλοντικά για Noria/Devias)
            _ => FallbackWorldObjectRules.Instance
        };
    }
}