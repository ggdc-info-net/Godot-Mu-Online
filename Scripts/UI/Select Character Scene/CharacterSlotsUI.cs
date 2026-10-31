using Godot;
using System.Collections.Generic;

public partial class CharacterSlotsUI : Control
{
    private VBoxContainer _slotsContainer;
    private Button _startButton;

    public record CharacterInfo(string Name, int Level);

    [Signal] public delegate void SlotSelectedEventHandler(int index, bool isVacant);
    [Signal] public delegate void StartGameClickedEventHandler();

    private List<CharacterInfo> _characters = new();
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        // Σταθερό μέγεθος και θέση δεξιά στην οθόνη
        CustomMinimumSize = new Vector2(300, 580);
        Position = new Vector2(920, 100); 

        // Φόντο πλαισίου
        var bgPanel = new Panel();
        bgPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bgPanel);

        // Κεντρικό VBox για τη διάταξη (Slots πάνω, Start Game κάτω)
        var mainVBox = new VBoxContainer();
        mainVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mainVBox.AddThemeConstantOverride("separation", 10);
        mainVBox.OffsetLeft = 15;
        mainVBox.OffsetRight = -15;
        mainVBox.OffsetTop = 15;
        mainVBox.OffsetBottom = -15;
        AddChild(mainVBox);

        // ScrollContainer για τα slots (για να μην ξεφεύγουν αν είναι πολλά)
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.Expand;
        scroll.CustomMinimumSize = new Vector2(270, 460);
        mainVBox.AddChild(scroll);

        _slotsContainer = new VBoxContainer();
        _slotsContainer.SizeFlagsHorizontal = SizeFlags.Expand;
        _slotsContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_slotsContainer);

        // Κουμπί Start Game στο κάτω μέρος (χωρίς επικάλυψη)
        _startButton = new Button();
        _startButton.Text = "START GAME";
        _startButton.CustomMinimumSize = new Vector2(270, 45);
        _startButton.Disabled = true;
        _startButton.Pressed += () => EmitSignal(SignalName.StartGameClicked);
        mainVBox.AddChild(_startButton);

        RefreshSlots();
    }

    public void SetCharacters(List<CharacterInfo> characters)
    {
        _characters = characters;
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (_slotsContainer == null) return;

        foreach (Node child in _slotsContainer.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            var slotButton = new Button();
            slotButton.CustomMinimumSize = new Vector2(250, 40);
            slotButton.MouseFilter = MouseFilterEnum.Stop; // Διασφαλίζει ότι δέχεται κλικ

            if (i < _characters.Count)
            {
                var charInfo = _characters[i];
                slotButton.Text = $"{charInfo.Name} (Lv. {charInfo.Level})";
                if (_selectedIndex == index)
                {
                    slotButton.AddThemeColorOverride("font_color", Colors.Cyan);
                }
            }
            else
            {
                slotButton.Text = "Vacant Character Slot";
                slotButton.AddThemeColorOverride("font_color", Colors.Gray);
            }

            slotButton.Pressed += () => OnSlotPressed(index);
            _slotsContainer.AddChild(slotButton);
        }

        bool hasValidSelection = _selectedIndex >= 0 && _selectedIndex < _characters.Count;
        _startButton.Disabled = !hasValidSelection;
    }

    private void OnSlotPressed(int index)
    {
        _selectedIndex = index;
        bool isVacant = index >= _characters.Count;
        
        GD.Print($"[CharacterSlotsUI] Slot {index} clicked. Is Vacant: {isVacant}");
        RefreshSlots();
        
        EmitSignal(SignalName.SlotSelected, index, isVacant);
    }
}