using Godot;
using System;
using System.Diagnostics;
using SysEnv = System.Environment;

public partial class PerformanceOverlay : CanvasLayer
{
    private Label _statsLabel;
    private float _updateTimer = 0f;
    private Process _currentProcess;
    
    private TimeSpan _lastCpuTime;
    private DateTime _lastTime;
    private double _cpuUsagePercent = 0.0;
    private int _processorCount;

    public override void _Ready()
    {
        Layer = 100;
        _currentProcess = Process.GetCurrentProcess();
        _processorCount = SysEnv.ProcessorCount;
        
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastTime = DateTime.UtcNow;

        // 1. Δημιουργία PanelContainer για το "κουτάκι"
        var panelContainer = new PanelContainer();
        panelContainer.Name = "StatsPanel";
        
        // Τοποθέτηση Επάνω Δεξιά (Anchor & Offsets)
        panelContainer.AnchorLeft = 1.0f;
        panelContainer.AnchorRight = 1.0f;
        panelContainer.AnchorTop = 0.0f;
        panelContainer.AnchorBottom = 0.0f;
        
        panelContainer.OffsetLeft = -220;
        panelContainer.OffsetRight = -20;
        panelContainer.OffsetTop = 20;
        panelContainer.OffsetBottom = 200;

        // Στυλ για το κουτάκι (ημιδιαφανές μαύρο background με StyleBoxFlat)
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0, 0, 0, 0.6f); // Μαύρο με 60% διαφάνεια
        styleBox.CornerRadiusTopLeft = 6;
        styleBox.CornerRadiusTopRight = 6;
        styleBox.CornerRadiusBottomLeft = 6;
        styleBox.CornerRadiusBottomRight = 6;
        // Padding εσωτερικά στο κουτάκι
        styleBox.ContentMarginLeft = 10;
        styleBox.ContentMarginRight = 10;
        styleBox.ContentMarginTop = 10;
        styleBox.ContentMarginBottom = 10;
        
        panelContainer.AddThemeStyleboxOverride("panel", styleBox);

        // 2. Δημιουργία Label για το κείμενο (πιο μικρά γράμματα)
        _statsLabel = new Label();
        _statsLabel.Name = "StatsLabel";
        
        var fontColor = Colors.LightGreen;
        _statsLabel.AddThemeColorOverride("font_color", fontColor);
        _statsLabel.AddThemeFontSizeOverride("font_size", 13); // Μειωμένο μέγεθος γραμμάτων

        // Προσθήκη του Label μέσα στο PanelContainer και έπειτα στη σκηνή
        panelContainer.AddChild(_statsLabel);
        AddChild(panelContainer);
    }

    public override void _Process(double delta)
    {
        _updateTimer += (float)delta;
        if (_updateTimer < 0.2f) return;

        // Υπολογισμός CPU Usage %
        try
        {
            DateTime currentTime = DateTime.UtcNow;
            TimeSpan currentCpuTime = _currentProcess.TotalProcessorTime;

            double cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            double totalPassedMs = (currentTime - _lastTime).TotalMilliseconds * _processorCount;

            if (totalPassedMs > 0)
            {
                _cpuUsagePercent = (cpuUsedMs / totalPassedMs) * 100.0;
            }

            _lastCpuTime = currentCpuTime;
            _lastTime = currentTime;
        }
        catch
        {
            _cpuUsagePercent = 0.0;
        }

        _updateTimer = 0f;

        // Godot Metrics
        int fps = (int)Performance.GetMonitor(Performance.Monitor.TimeFps);
        int drawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        int primitives = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        int objects = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        
        // VRAM Metrics (MB)
        long totalVideoMem = (long)Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024 * 1024);
        long textureMem = (long)Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024 * 1024);
        long bufferMem = (long)Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed) / (1024 * 1024);

        // System RAM Metric (MB)
        _currentProcess.Refresh();
        long sysRamMb = _currentProcess.WorkingSet64 / (1024 * 1024);

        // Μορφοποίηση κειμένου
        _statsLabel.Text = 
            $"--- PERFORMANCE STATS ---\n" +
            $"FPS: {fps}\n" +
            $"CPU Usage: {_cpuUsagePercent:F1}%\n" +
            $"System RAM: {sysRamMb} MB\n" +
            $"Draw Calls: {drawCalls}\n" +
            $"Vertices (Prim): {primitives:N0}\n" +
            $"Objects In Frame: {objects}\n" +
            $"Total VRAM: {totalVideoMem} MB\n" +
            $" ├── Textures: {textureMem} MB\n" +
            $" └── Buffers: {bufferMem} MB";
    }
}