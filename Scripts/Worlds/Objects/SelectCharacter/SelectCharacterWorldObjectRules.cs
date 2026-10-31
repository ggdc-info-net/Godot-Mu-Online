using Client.Main.Utils;
using Godot;

public sealed class SelectCharacterWorldObjectRules : IWorldObjectRules
{
    public static SelectCharacterWorldObjectRules Instance { get; } = new();

    private SelectCharacterWorldObjectRules() { }

    public bool IsSpecialObject(short type)
    {
        // Αν δεν έχεις ειδικά αντικείμενα (όπως φωτιές/πύλες) εδώ, επέστρεψε πάντα false
        return false;
    }

    public void ApplyInstanceRules(short type, Node3D instanceNode)
    {
        if (instanceNode == null) return;
        
        // Εδώ μπορείς να προσθέσεις γενικούς κανόνες αν χρειάζεται
    }
}