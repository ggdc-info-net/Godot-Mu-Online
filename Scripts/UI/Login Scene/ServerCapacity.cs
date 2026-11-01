using System;
using Client.Main.Content;
using Godot;

namespace Client.Main.Controls.UI.Login
{
    public partial class ServerCapacity : Button
    {
        private Label _label;
        private TextureRect _textureRect;
        private ColorRect _statusBar; // Αντίστοιχο για τη μπάρα φόρτωσης στο classic layout
        private Texture2D _sourceTexture;

        private byte _gauge = 0;
        [Export]
        public byte Gauge
        {
            get => _gauge;
            set
            {
                _gauge = value;
                UpdateVisualState();
            }
        }

        public bool Available => _gauge < 100;

        private byte _index;
        public byte Index
        {
            get => _index;
            set => _index = value;
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
            CustomMinimumSize = new Vector2(192, 26);
            Size = CustomMinimumSize;
            Flat = true; // Απενεργοποιούμε το κλασικό φόντο του Godot Button
            Position = new Vector2(609, 300);
            // Δημιουργία TextureRect για το background/atlas
            _textureRect = new TextureRect
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                StretchMode = TextureRect.StretchModeEnum.Keep,
                MouseFilter = Control.MouseFilterEnum.Ignore // Να μην εμποδίζει τα κλικ του κουμπιού
            };
            AddChild(_textureRect);

            // Δημιουργία Label για το όνομα του server
            _label = new Label
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_label);

            // Δημιουργία μπάρας κατάστασης (status bar)
            _statusBar = new ColorRect
            {
                Position = new Vector2(9, 19),
                CustomMinimumSize = new Vector2(0, 4),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_statusBar);

            // Σύνδεση Godot Signals για τα states του ποντικιού
            MouseEntered += UpdateVisualState;
            MouseExited += UpdateVisualState;
            ButtonDown += UpdateVisualState;
            ButtonUp += UpdateVisualState;

            // Φόρτωση υφής (ανάλογα με τον TextureLoader σου)
            _sourceTexture = await TextureLoader.Instance.PrepareAndGetTexture("Interface/server_b2_all.tga");
            
            UpdateVisualState();
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            // Αντιστοίχιση της διαθεσιμότητας στο Disabled του κουμπιού της Godot
            Disabled = !Available;

            // Ενημέρωση μεγέθους μπάρας φόρτωσης ανάλογα με το Gauge
            float fillWidth = (CustomMinimumSize.X - 20) * Math.Clamp(_gauge, (byte)0, (byte)100) / 100f;
            _statusBar.Size = new Vector2(fillWidth, _statusBar.Size.Y);
            
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_sourceTexture == null) return;

            if (!Available)
            {
                TileY = 2; // Γεμάτος / Ανενεργός
            }
            else if (ButtonPressed || IsHovered())
            {
                TileY = 1; // Πατημένος ή Hover
            }
            else
            {
                TileY = 0; // Κανονικός
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
            }

            // Κόβει το αντίστοιχo κομμάτι ύψους 26 pixels από το atlas
            atlas.Region = new Rect2(0, _tileY * 26, 192, 26);
        }
    }
}