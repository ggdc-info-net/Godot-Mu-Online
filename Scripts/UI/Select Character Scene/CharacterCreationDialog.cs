using Godot;

public partial class CharacterCreationDialog : Control
{
    private LineEdit _nameInput;
    private Label _classLabel;
    private Label _descLabel;

    private readonly string[] _classNames = { "Dark Wizard", "Dark Knight", "Fairy Elf", "Magic Gladiator", "Dark Lord", "Summoner", "Rage Fighter" };
    private readonly string[] _classDescriptions = {
        "Masters of magical destruction.\nHigh magic damage, low defense.",
        "Warriors of strength and honor.\nHigh health and physical damage.",
        "Agile archers and healers.\nHigh agility, support abilities.",
        "Hybrid warriors with magic.\nBalanced melee and magic skills.",
        "Commanders with dark powers.\nSummons pets and commands armies.",
        "Mystics with curse powers.\nCurses enemies and summons.",
        "Hand-to-hand combat masters.\nHighest HP, powerful combos."
    };
    private int _selectedIndex = 0;

    [Signal] public delegate void CharacterCreateRequestedEventHandler(string characterName, int classNumber);
    [Signal] public delegate void CancelRequestedEventHandler();

    public override void _Ready()
    {
        // Καλύπτει όλη την οθόνη με διαφάνεια/μπλοκάρισμα κλικ από πίσω
        AnchorRight = 1.0f;
        AnchorBottom = 1.0f;
        MouseFilter = MouseFilterEnum.Stop;

        // Σκοτεινό φόντο πίσω από το παράθυρο
        var bg = new ColorRect();
        bg.AnchorRight = 1.0f; bg.AnchorBottom = 1.0f;
        bg.Color = new Color(0, 0, 0, 0.6f);
        AddChild(bg);

        // Κεντρικό Panel
        var panel = new PanelContainer();
        panel.AnchorLeft = 0.5f; panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f; panel.AnchorBottom = 0.5f;
        panel.CustomMinimumSize = new Vector2(450, 380);
        panel.Position = new Vector2(-225, -190);
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        // Τίτλος
        var title = new Label { Text = "CREATE CHARACTER", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(title);

        // Name Input
        vbox.AddChild(new Label { Text = "Character Name (3-10 chars):" });
        _nameInput = new LineEdit { MaxLength = 10, PlaceholderText = "Enter name..." };
        _nameInput.TextChanged += OnNameTextChanged;
        vbox.AddChild(_nameInput);

        // Class Selection
        vbox.AddChild(new Label { Text = "Select Class:" });
        
        var classHBox = new HBoxContainer();
        var prevBtn = new Button { Text = "<", CustomMinimumSize = new Vector2(40, 40) };
        prevBtn.Pressed += () => ChangeClass(-1);
        classHBox.AddChild(prevBtn);

        _classLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.Expand };
        _classLabel.AddThemeFontSizeOverride("font_size", 16);
        classHBox.AddChild(_classLabel);

        var nextBtn = new Button { Text = ">", CustomMinimumSize = new Vector2(40, 40) };
        nextBtn.Pressed += () => ChangeClass(1);
        classHBox.AddChild(nextBtn);
        vbox.AddChild(classHBox);

        // Description
        _descLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 60) };
        vbox.AddChild(_descLabel);

        // Buttons (Create / Cancel)
        var btnHBox = new HBoxContainer();
        btnHBox.AddThemeConstantOverride("separation", 10);
        
        var createBtn = new Button { Text = "CREATE", SizeFlagsHorizontal = SizeFlags.Expand };
        createBtn.Pressed += OnCreatePressed;
        btnHBox.AddChild(createBtn);

        var cancelBtn = new Button { Text = "CANCEL", SizeFlagsHorizontal = SizeFlags.Expand };
        cancelBtn.Pressed += () => {
            EmitSignal(SignalName.CancelRequested);
            QueueFree();
        };
        btnHBox.AddChild(cancelBtn);
        
        vbox.AddChild(btnHBox);

        UpdateDisplay();
        _nameInput.GrabFocus();
    }

    private void ChangeClass(int dir)
    {
        _selectedIndex = (_selectedIndex + dir + _classNames.Length) % _classNames.Length;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _classLabel.Text = _classNames[_selectedIndex];
        _descLabel.Text = _classDescriptions[_selectedIndex];
    }

    private void OnNameTextChanged(string newText)
    {
        string sanitized = "";
        foreach (char c in newText)
        {
            if (char.IsLetterOrDigit(c) && sanitized.Length < 10)
                sanitized += c;
        }
        if (sanitized != newText)
            _nameInput.Text = sanitized;
    }

    private void OnCreatePressed()
    {
        string name = _nameInput.Text.Trim();
        if (name.Length < 3)
        {
            GD.PrintErr("Name must be at least 3 characters.");
            return;
        }
        EmitSignal(SignalName.CharacterCreateRequested, name, _selectedIndex);
        QueueFree();
    }
}