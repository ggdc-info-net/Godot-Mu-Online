using Client.Main.Utils;
using Godot;
using System.Collections.Generic;

public sealed class SelectCharacterWorldObjectRules : IWorldObjectRules
{
    public static SelectCharacterWorldObjectRules Instance { get; } = new();

    private SelectCharacterWorldObjectRules() { }

    // Κρατάμε ποια types έχουμε ήδη τυπώσει για να μη γεμίσει η κονσόλα 200 γραμμές
    private readonly HashSet<short> _loggedTypes = new();

    public bool IsSpecialObject(short type)
    {
        // Δοκίμασε και το 18 και το 19 (γιατί το Object19.bmd έχει type 18!)
        return type is 6 or 14 or 15 or 16 or 17 or 18;
    }

    public void ApplyInstanceRules(short type, Node3D instanceNode)
    {
        GD.Print($"[World 94 Special Rule Triggered] Deleting Type: {type}");

        if (type is 6 or 14 or 15 or 16 or 17 or 18)
        {
            instanceNode.Visible = false;
            instanceNode.CallDeferred(Node.MethodName.QueueFree);
            return;
        }
    }
}