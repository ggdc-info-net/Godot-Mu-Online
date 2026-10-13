using Godot;

public interface IWorldObjectRules
{
    // Επιστρέφει true αν το αντικείμενο είναι special και πρέπει να εξαιρεθεί από το MultiMesh
    bool IsSpecialObject(short type);

    // Εφαρμόζει τα αντίστοιχα effects/φώτα πάνω στο Node του αντικειμένου
    void ApplyInstanceRules(short type, Node3D instanceNode);
}