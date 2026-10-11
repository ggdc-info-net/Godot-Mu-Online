using Godot;

public partial class PerformanceOverlay : CanvasLayer
{
    private Label _statsLabel;
    private float _updateTimer = 0f;

    public override void _Ready()
    {
        // Δημιουργία Layer για να μένει σταθερό στην οθόνη ανεξάρτητα από την κάμερα
        Layer = 100;

        // Δημιουργία Label για το κείμενο
        _statsLabel = new Label();
        _statsLabel.Name = "StatsLabel";
        
        // Τοποθέτηση Επάνω Δεξιά (Anchor & Offsets)
        _statsLabel.AnchorLeft = 1.0f;
        _statsLabel.AnchorRight = 1.0f;
        _statsLabel.AnchorTop = 0.0f;
        _statsLabel.AnchorBottom = 0.0f;
        
        // Offset από την πάνω-δεξιά γωνία (π.χ. X: -220, Y: 20)
        _statsLabel.OffsetLeft = -220;
        _statsLabel.OffsetRight = -20;
        _statsLabel.OffsetTop = 20;
        _statsLabel.OffsetBottom = 150;

        // Στυλ κειμένου (μαύρο background για ευανάγνωστο text)
        var theme = new Theme();
        var fontColor = Colors.LightGreen;
        
        _statsLabel.AddThemeColorOverride("font_color", fontColor);
        _statsLabel.AddThemeFontSizeOverride("font_size", 16);

        AddChild(_statsLabel);
    }

    public override void _Process(double delta)
    {
        // Ανανέωση ανά 0.2 δευτερόλεπτα για να μη τρεμοπαίζει υπερβολικά το κείμενο
        _updateTimer += (float)delta;
        if (_updateTimer < 0.2f) return;
        _updateTimer = 0f;

        // Ανάγνωση δεδομένων από την Godot Performance Singleton
        int fps = (int)Performance.GetMonitor(Performance.Monitor.TimeFps);
        int drawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        int primitives = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        int objects = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        long videoMem = (long)Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024 * 1024); // σε MB

        // Μορφοποίηση κειμένου
        _statsLabel.Text = 
            $"--- PERFORMANCE STATS ---\n" +
            $"FPS: {fps}\n" +
            $"Draw Calls: {drawCalls}\n" +
            $"Vertices (Prim): {primitives:N0}\n" +
            $"Objects In Frame: {objects}\n" +
            $"Video Mem: {videoMem} MB";
    }
}