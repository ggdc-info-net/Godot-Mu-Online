using Godot;
using Client.Main.Content;

namespace Client.Main.Controls.UI.Login
{
    public partial class ServerButton : Button
    {
        private Label _label;
        private TextureRect _textureRect;
        private Texture2D _sourceTexture;

        private byte _index;
        public byte Index
        {
            get => _index;
            set => _index = value;
        }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                UpdateVisualState();
            }
        }

        public new string Name
        {
            get => _label?.Text ?? string.Empty;
            set
            {
                if (_label != null) _label.Text = value;
            }
        }

        private int _tileY = 0;
        public int TileY
        {
            get => _tileY;
            set
            {
                _tileY = value;
                UpdateTextureRegion();
            }
        }

        public override async void _Ready()
        {
            CustomMinimumSize = new Vector2(110, 26);
            Size = CustomMinimumSize;
            Flat = true;
            Position = new Vector2(500, 300);
            
            _textureRect = new TextureRect
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                StretchMode = TextureRect.StretchModeEnum.Keep
            };
            AddChild(_textureRect);

            _label = new Label
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector2(0, 0)
            };
            AddChild(_label);

            // Σύνδεση με τα built-in signals του Godot Button για τα states
            MouseEntered += UpdateVisualState;
            MouseExited += UpdateVisualState;
            ButtonDown += UpdateVisualState;
            ButtonUp += UpdateVisualState;

            // 📌 ΣΥΝΔΕΣΗ ΤΟΥ CLICK ΜΕ ΤΗ ΜΕΘΟΔΟ ΦΟΡΤΩΣΗΣ ΣΚΗΝΗΣ
            Pressed += OnServerButtonPressed;

            _label.AddThemeColorOverride("font_color", Colors.Gray);
            _label.AddThemeFontSizeOverride("font_size", 14);
            
            _sourceTexture = await TextureLoader.Instance.PrepareAndGetTexture("Interface/cha_bt.tga");
            UpdateVisualState();

            Name = "Server";
        }

        private void OnServerButtonPressed()
        {
            Disabled = true; // Κλειδώνει το κουμπί για να μην πατηθεί 2 φορές
            GD.Print("[ServerButton] Clicked! Switching state to SelectCharacter...");

            GameManager.Instance.ChangeGameState(GameManager.GameState.SelectCharacter);
        }

        private void UpdateVisualState()
        {
            if (_sourceTexture == null) return;

            if (Selected)
            {
                TileY = 3;
            }
            else if (ButtonPressed)
            {
                TileY = 2;
            }
            else if (IsHovered())
            {
                TileY = 1;
            }
            else
            {
                TileY = 0;
            }
        }

        private void UpdateTextureRegion()
        {
            if (_sourceTexture == null) return;

            if (_textureRect.Texture is not AtlasTexture atlas)
            {
                atlas = new AtlasTexture();
                atlas.Atlas = _sourceTexture;
                _textureRect.Texture = atlas;
                _textureRect.StretchMode = TextureRect.StretchModeEnum.Keep;
            }

            atlas.Region = new Rect2(0, _tileY * 26, 110, 26);
        }
    }
}