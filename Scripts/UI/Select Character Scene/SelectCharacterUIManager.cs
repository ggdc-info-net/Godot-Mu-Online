using Godot;
using System.Collections.Generic;

public partial class SelectCharacterUIManager : Control
{
    private CharacterSlotsUI _slotsUI;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var canvasLayer = new CanvasLayer();
        AddChild(canvasLayer);

        _slotsUI = new CharacterSlotsUI();
        canvasLayer.AddChild(_slotsUI);

        // Δοκιμαστικά δεδομένα (1 χαρακτήρας, 9 κενά slots)
        var testChars = new List<CharacterSlotsUI.CharacterInfo>
        {
            new("DarkWizard99", 150)
        };
        _slotsUI.SetCharacters(testChars);

        // Ακρόαση κλικ στα slots
        _slotsUI.SlotSelected += (index, isVacant) => {
            if (isVacant)
            {
                GD.Print($"[UIManager] Κενό slot {index} πατήθηκε. Άνοιγμα CharacterCreationDialog...");
                
                var dialogLayer = new CanvasLayer();
                dialogLayer.Layer = 10;

                var createDialog = new CharacterCreationDialog();
                createDialog.CharacterCreateRequested += (charName, classNum) => {
                    GD.Print($"[UIManager] Δημιουργία χαρακτήρα: {charName} (Κλάση: {classNum}) στο slot {index}");
                    dialogLayer.QueueFree();
                };

                createDialog.CancelRequested += () => {
                    dialogLayer.QueueFree();
                };
                
                dialogLayer.AddChild(createDialog);
                AddChild(dialogLayer);
            }
            else
            {
                GD.Print($"[UIManager] Επιλέχθηκε ο υπάρχων χαρακτήρας στο slot {index}! Το Start Game πρέπει να είναι ενεργό.");
            }
        };

        _slotsUI.StartGameClicked += () => {
            GD.Print("[UIManager] >>> START GAME CLICKED! Changing to GameScene... <<<");
            
            if (GameManager.Instance != null)
            {
                // Ο GameManager αναλαμβάνει τη μετάβαση στην InGame κατάσταση/σκηνή
                GameManager.Instance.ChangeGameState(GameManager.GameState.InGame);
                
            }
            else
            {
                // Εναλλακτικά, αν φορτώνεις απευθείας τη σκηνή με αρχείο:
                GetTree().ChangeSceneToFile("res://Scenes/Game Scene/GameScene.tscn");
            }
        };
        
        GD.Print("[SelectCharacterUIManager] Initialized and events connected successfully.");
    }
}