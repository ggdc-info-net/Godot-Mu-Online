using Godot;

namespace MuGodot.Objects.Worlds; // Βεβαιώσου ότι ταιριάζει με το namespace του project σου

public sealed class FallbackWorldObjectRules : IWorldObjectRules
{
    public static FallbackWorldObjectRules Instance { get; } = new();

    private FallbackWorldObjectRules()
    {
    }

    public bool IsSpecialObject(short type)
    {
        // Από προεπιλογή, κανένα αντικείμενο δεν είναι special στο fallback
        return false;
    }

    public void ApplyInstanceRules(short type, Node3D instanceNode)
    {
        // Καμία ενέργεια από προεπιλογή
    }
}