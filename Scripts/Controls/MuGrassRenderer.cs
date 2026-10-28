using Client.Main.Content;
using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MuGodot;

public sealed class MuGrassRenderer
{
    private static readonly Shader GrassShader = GD.Load<Shader>("res://Shaders/Lorencia/grass.gdshader");
    private const int TerrainSize = Client.Main.Constants.TERRAIN_SIZE;
    private const float ScaleFactor = Client.Main.Constants.TERRAIN_SCALE; // 100f

    // Αποστάσεις σε World Units
    private const float GrassNearSq = (30f * ScaleFactor) * (30f * ScaleFactor);
    private const float GrassMidSq = (40f * ScaleFactor) * (40f * ScaleFactor);
    private const float GrassFarSq = (50f * ScaleFactor) * (50f * ScaleFactor);

    // Βασικές διαστάσεις λεπίδας
    private const float GrassBladeBaseW = 1.30f * ScaleFactor;
    private const float GrassBladeBaseH = 0.85f * ScaleFactor;
    private const float HeightOffset = 0f;
    private const float GrassUWidth = 0.30f;

    private readonly struct GrassTile
    {
        public GrassTile(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
    }

    private readonly struct VisibleGrassTile
    {
        public VisibleGrassTile(int x, int y, float distSq) { X = x; Y = y; DistSq = distSq; }
        public int X { get; }
        public int Y { get; }
        public float DistSq { get; }
    }

    private readonly TerrainControl _terrain;
    private readonly List<GrassTile> _grassTiles = new(32768);
    private readonly List<VisibleGrassTile> _visibleTiles = new(8192);
    private readonly bool[] _grassMask = new bool[TerrainSize * TerrainSize];
    private readonly HashSet<byte> _grassTextureIndices = new() { 0 };

    private Node3D? _root;
    private MultiMeshInstance3D? _instanceNode;
    private MultiMesh? _activeMultiMesh;
    private MultiMesh? _stagingMultiMesh;
    private ShaderMaterial? _material;

    private float _refreshTimer;
    private float _windTime;
    private Vector2 _lastRebuildCameraPos;
    private bool _hasLastCameraPos;
    private bool _initialized;
    private bool _rebuildInProgress;
    private int _rebuildTileCursor;
    private int _rebuildInstanceCount;
    private float _rebuildFocalPx;
    private bool _rebuildHasScreenCull;
    private bool _rebuildQueued;
    private Vector3 _queuedCameraPosition;

    public int MaxInstances { get; set; } = 12000;
    public float RefreshIntervalSeconds { get; set; } = 0.20f;
    public float WindSpeed { get; set; } = 1.20f;
    public float WindStrength { get; set; } = 0.85f;
    public float GrassBrightness { get; set; } = 1.15f;
    public float AlphaCutoff { get; set; } = 0.40f;
    public float DensityScale { get; set; } = 0.35f;
    public float BladeWidthScale { get; set; } = 1.15f;
    public float BladeHeightScale { get; set; } = 0.95f;
    public float MaxRandomScale { get; set; } = 1.40f;
    public float RebuildCameraMoveThreshold { get; set; } = 150f;
    public bool ProgressiveRebuild { get; set; } = true;
    public int RebuildTilesPerFrame { get; set; } = 250;

    public MuGrassRenderer(TerrainControl terrain)
    {
        _terrain = terrain;
    }

    public void ConfigureGrass(float brightness, params byte[] textureIndices)
    {
        GrassBrightness = brightness;
        if (textureIndices == null || textureIndices.Length == 0)
            return;

        _grassTextureIndices.Clear();
        for (int i = 0; i < textureIndices.Length; i++)
            _grassTextureIndices.Add(textureIndices[i]);
    }

    public async Task BuildAsync(int worldIndex, Node3D parent, Vector3 cameraPosition)
    {
        Clear();

        var texture = await LoadGrassTextureAsync(worldIndex);
        if (texture == null)
        {
            GD.PrintErr($"[Grass] Texture missing for World{worldIndex}, skipping grass.");
            return;
        }

        BuildGrassTileList();
        if (_grassTiles.Count == 0)
        {
            GD.Print($"[Grass] No eligible grass tiles for World{worldIndex}.");
            return;
        }

        _root = new Node3D { Name = "Grass" };
        parent.AddChild(_root);

        var mesh = CreateBladeMesh();
        _material = new ShaderMaterial { Shader = GrassShader };
        _material.SetShaderParameter("grass_texture", texture);
        _material.SetShaderParameter("wind_speed", WindSpeed);
        _material.SetShaderParameter("wind_strength", WindStrength);
        _material.SetShaderParameter("alpha_scissor", Mathf.Clamp(AlphaCutoff, 0.05f, 0.95f));
        mesh.SurfaceSetMaterial(0, _material);

        _activeMultiMesh = CreateMultiMesh(mesh);
        _stagingMultiMesh = CreateMultiMesh(mesh);

        _instanceNode = new MultiMeshInstance3D
        {
            Name = "GrassInstances",
            Multimesh = _activeMultiMesh,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 16384f
        };

        _root.AddChild(_instanceNode);
        _initialized = true;

        StartRebuild(cameraPosition);
        ContinueRebuild(int.MaxValue);
        GD.Print($"[Grass] Initialized {_grassTiles.Count} candidate tiles, spawned {_activeMultiMesh.VisibleInstanceCount} blades.");
    }

    public void Update(double delta, Vector3 cameraPosition)
    {
        if (!_initialized || _material == null || _activeMultiMesh == null || _stagingMultiMesh == null)
            return;

        _windTime += (float)delta;
        _material.SetShaderParameter("wind_time", _windTime);
        _material.SetShaderParameter("wind_speed", WindSpeed);
        _material.SetShaderParameter("wind_strength", WindStrength);
        _material.SetShaderParameter("alpha_scissor", Mathf.Clamp(AlphaCutoff, 0.05f, 0.95f));

        _refreshTimer += (float)delta;
        if (_refreshTimer >= RefreshIntervalSeconds)
        {
            _refreshTimer = 0f;
            var currentPos = new Vector2(cameraPosition.X, MathF.Abs(cameraPosition.Z));
            bool shouldRebuild = !_hasLastCameraPos;
            if (!shouldRebuild)
            {
                float thresholdSq = RebuildCameraMoveThreshold * RebuildCameraMoveThreshold;
                shouldRebuild = _lastRebuildCameraPos.DistanceSquaredTo(currentPos) >= thresholdSq;
            }

            if (shouldRebuild)
            {
                if (_rebuildInProgress)
                {
                    _rebuildQueued = true;
                    _queuedCameraPosition = cameraPosition;
                }
                else
                {
                    StartRebuild(cameraPosition);
                }
            }
        }

        if (_rebuildInProgress)
        {
            if (ProgressiveRebuild)
                ContinueRebuild(RebuildTilesPerFrame);
            else
                ContinueRebuild(int.MaxValue);
        }
        else if (_rebuildQueued)
        {
            StartRebuild(_queuedCameraPosition);
            _rebuildQueued = false;
            if (!ProgressiveRebuild)
                ContinueRebuild(int.MaxValue);
        }
    }

    public void Clear()
    {
        _initialized = false;
        _refreshTimer = 0f;
        _windTime = 0f;
        _hasLastCameraPos = false;
        _rebuildInProgress = false;
        _rebuildQueued = false;
        _rebuildTileCursor = 0;
        _rebuildInstanceCount = 0;
        _grassTiles.Clear();
        _visibleTiles.Clear();

        if (_root != null && GodotObject.IsInstanceValid(_root))
            _root.QueueFree();

        _root = null;
        _instanceNode = null;
        _activeMultiMesh = null;
        _stagingMultiMesh = null;
        _material = null;
    }

    private void BuildGrassTileList()
    {
        _grassTiles.Clear();
        Array.Clear(_grassMask, 0, _grassMask.Length);

        for (int y = 0; y < TerrainSize - 1; y++)
        {
            for (int x = 0; x < TerrainSize - 1; x++)
            {
                if (_terrain.IsPureGrassTile(x, y, _grassTextureIndices))
                {
                    _grassTiles.Add(new GrassTile(x, y));
                    _grassMask[y * TerrainSize + x] = true;
                }
            }
        }
    }

    private void StartRebuild(Vector3 cameraPosition)
    {
        if (_activeMultiMesh == null || _stagingMultiMesh == null)
            return;

        if (_activeMultiMesh.InstanceCount != MaxInstances)
            _activeMultiMesh.InstanceCount = MaxInstances;

        if (_stagingMultiMesh.InstanceCount != MaxInstances)
            _stagingMultiMesh.InstanceCount = MaxInstances;

        float camWorldX = cameraPosition.X;
        float camWorldY = MathF.Abs(cameraPosition.Z);

        _lastRebuildCameraPos = new Vector2(camWorldX, camWorldY);
        _hasLastCameraPos = true;

        _rebuildFocalPx = 0f;
        _rebuildHasScreenCull = false;
        var viewport = _instanceNode?.GetViewport();
        var camera = viewport?.GetCamera3D();
        if (viewport != null && camera != null)
        {
            float viewH = Mathf.Max(1f, viewport.GetVisibleRect().Size.Y);
            float fovRad = Mathf.DegToRad(camera.Fov);
            _rebuildFocalPx = viewH / (2f * Mathf.Tan(fovRad * 0.5f));
            _rebuildHasScreenCull = _rebuildFocalPx > 0.001f;
        }

        CollectVisibleTiles(camWorldX, camWorldY);
        _visibleTiles.Sort(static (a, b) => a.DistSq.CompareTo(b.DistSq));

        _rebuildTileCursor = 0;
        _rebuildInstanceCount = 0;
        _rebuildInProgress = true;
        _rebuildQueued = false;
    }

    private void CollectVisibleTiles(float camWorldX, float camWorldY)
    {
        int centerX = Mathf.Clamp((int)MathF.Floor(camWorldX / ScaleFactor), 0, TerrainSize - 1);
        int centerY = Mathf.Clamp((int)MathF.Floor(camWorldY / ScaleFactor), 0, TerrainSize - 1);

        int farRadius = (int)MathF.Ceiling(MathF.Sqrt(GrassFarSq) / ScaleFactor);
        int minX = Math.Max(0, centerX - farRadius);
        int maxX = Math.Min(TerrainSize - 2, centerX + farRadius);
        int minY = Math.Max(0, centerY - farRadius);
        int maxY = Math.Min(TerrainSize - 2, centerY + farRadius);

        _visibleTiles.Clear();
        for (int y = minY; y <= maxY; y++)
        {
            int rowBase = y * TerrainSize;
            for (int x = minX; x <= maxX; x++)
            {
                if (!_grassMask[rowBase + x])
                    continue;

                float txCenterWorld = (x + 0.5f) * ScaleFactor;
                float tyCenterWorld = (y + 0.5f) * ScaleFactor;
                float dx = camWorldX - txCenterWorld;
                float dy = camWorldY - tyCenterWorld;
                float distSq = dx * dx + dy * dy;

                if (distSq >= GrassFarSq)
                    continue;

                _visibleTiles.Add(new VisibleGrassTile(x, y, distSq));
            }
        }
    }

    private void ContinueRebuild(int tilesBudget)
    {
        if (_stagingMultiMesh == null || !_rebuildInProgress)
            return;

        int budget = tilesBudget >= int.MaxValue ? int.MaxValue : Math.Max(16, tilesBudget);
        int processedTiles = 0;

        while (_rebuildTileCursor < _visibleTiles.Count &&
               _rebuildInstanceCount < MaxInstances &&
               processedTiles < budget)
        {
            var tile = _visibleTiles[_rebuildTileCursor++];
            processedTiles++;

            int x = tile.X;
            int y = tile.Y;
            float distSq = tile.DistSq;

            int grassPerTile = GetGrassCount(distSq);
            if (grassPerTile == 0)
                continue;
            grassPerTile = ScaleGrassCount(grassPerTile);
            if (grassPerTile == 0)
                continue;
            if (_rebuildHasScreenCull)
                grassPerTile = AdjustGrassCountForScreenSize(distSq, grassPerTile, _rebuildFocalPx);
            if (grassPerTile == 0)
                continue;

            var tileLight = _terrain.GetTerrainLightColor(x, y);
            float bFactor = Mathf.Max(0.2f, GrassBrightness * 0.75f);
            var lit = new Color(
                Mathf.Clamp(tileLight.R * bFactor, 0.05f, 1f),
                Mathf.Clamp(tileLight.G * bFactor, 0.05f, 1f),
                Mathf.Clamp(tileLight.B * bFactor, 0.05f, 1f),
                1f);

            for (int i = 0; i < grassPerTile; i++)
            {
                if (_rebuildInstanceCount >= MaxInstances)
                    break;

                // 📌 Αυξάνουμε ελαφρώς το maxOffset (από 0.5f σε 0.68f) ώστε τα χόρτα 
                // να επεκτείνονται ελαφρώς προς τα όρια και να κλείνουν τα κενά ανάμεσα στα tiles,
                // παραμένοντας όμως ασφαλή εντός των καθαρών grass tiles.
                float maxOffset = 0.68f;

                float rx = (PseudoRandom(x, y, 17 + i) * 2f - 1f) * maxOffset;
                float ry = (PseudoRandom(x, y, 91 + i) * 2f - 1f) * maxOffset;

                float worldX = (x + 0.5f + rx) * ScaleFactor;
                float worldY = (y + 0.5f + ry) * ScaleFactor;

                float ground = _terrain.GetHeightInterpolated(worldX, worldY);
                float randomScale = Mathf.Lerp(0.85f, MaxRandomScale, PseudoRandom(x, y, 33 + i));
                float yaw = Mathf.DegToRad(45f + (PseudoRandom(x, y, 57 + i) - 0.5f) * 2f * 90f);

                Vector3 finalScale = new Vector3(
                    randomScale * BladeWidthScale,
                    randomScale * BladeHeightScale * 0.70f,
                    randomScale * BladeWidthScale
                );

                var basis = Basis.FromEuler(new Vector3(0f, yaw, 0f)).Scaled(finalScale);
                var transform = new Transform3D(basis, new Vector3(worldX, ground + HeightOffset, -worldY));
                _stagingMultiMesh.SetInstanceTransform(_rebuildInstanceCount, transform);
                _stagingMultiMesh.SetInstanceColor(_rebuildInstanceCount, lit);

                float u0 = PseudoRandom(x, y, 123 + i) * (1f - GrassUWidth);
                float phase01 = PseudoRandom(x, y, 211 + i);
                float amp01 = PseudoRandom(x, y, 307 + i);
                _stagingMultiMesh.SetInstanceCustomData(_rebuildInstanceCount, new Color(u0, GrassUWidth, phase01, amp01));
                _rebuildInstanceCount++;
            }
        }

        if (_rebuildTileCursor >= _visibleTiles.Count || _rebuildInstanceCount >= MaxInstances)
            CompleteRebuild();
    }

    private void CompleteRebuild()
    {
        if (_instanceNode == null || _activeMultiMesh == null || _stagingMultiMesh == null)
        {
            _rebuildInProgress = false;
            return;
        }

        _stagingMultiMesh.VisibleInstanceCount = _rebuildInstanceCount;
        var completed = _stagingMultiMesh;
        _stagingMultiMesh = _activeMultiMesh;
        _activeMultiMesh = completed;
        _stagingMultiMesh.VisibleInstanceCount = 0;
        _instanceNode.Multimesh = _activeMultiMesh;
        _rebuildInProgress = false;
    }

    private static int GetGrassCount(float distSq)
    {
        if (distSq < GrassNearSq) return 10;
        if (distSq < GrassMidSq) return 4;
        if (distSq < GrassFarSq) return 2;
        return 0;
    }

    private int ScaleGrassCount(int count)
    {
        float scaled = count * Mathf.Max(0.1f, DensityScale);
        return scaled < 0.99f ? 0 : Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    private int AdjustGrassCountForScreenSize(float distSq, int grassPerTile, float focalPx)
    {
        if (grassPerTile <= 0)
            return 0;

        float dist = Mathf.Sqrt(distSq);
        if (dist < 0.001f)
            return grassPerTile;
        float bladeHeight = GrassBladeBaseH * BladeHeightScale * MaxRandomScale;
        float pixelHeight = bladeHeight * focalPx / dist;

        if (distSq > GrassMidSq)
            pixelHeight *= 0.75f;

        if (pixelHeight < 1.2f) return 0;
        if (pixelHeight < 2.0f) return Mathf.Max(1, grassPerTile / 6);
        if (pixelHeight < 3.0f) return Mathf.Max(1, grassPerTile / 4);
        if (pixelHeight < 4.5f) return Mathf.Max(1, grassPerTile / 3);
        if (pixelHeight < 6.0f) return Mathf.Max(1, grassPerTile / 2);
        return grassPerTile;
    }

    private async Task<Texture2D?> LoadGrassTextureAsync(int worldIndex)
    {
        string file = worldIndex switch
        {
            3 => "TileGrass02.ozt",
            _ => "TileGrass01.ozt"
        };

        string baseDataPath = "F:/Program Files/MuCloneData/Data";
        string path = System.IO.Path.Combine(baseDataPath, $"World{worldIndex}", file);
        return await TextureLoader.Instance.PrepareAndGetTexture(path);
    }

    private static ArrayMesh CreateBladeMesh()
    {
        float halfW = GrassBladeBaseW * GrassUWidth * 0.5f;
        float h = GrassBladeBaseH;
        float sink = -3.0f; // Βύθιση βάσης στο έδαφος

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        int blades = 3;
        for (int b = 0; b < blades; b++)
        {
            float angle = b * (Mathf.Pi / 3.0f);
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            Vector3 p0 = new Vector3(-halfW * cosA, sink, -halfW * sinA);
            Vector3 p1 = new Vector3(halfW * cosA, sink, halfW * sinA);
            Vector3 p2 = new Vector3(-halfW * cosA, h, -halfW * sinA);
            Vector3 p3 = new Vector3(halfW * cosA, h, halfW * sinA);

            st.SetUV(new Vector2(0f, 1f)); st.AddVertex(p0);
            st.SetUV(new Vector2(1f, 1f)); st.AddVertex(p1);
            st.SetUV(new Vector2(0f, 0f)); st.AddVertex(p2);

            st.SetUV(new Vector2(1f, 1f)); st.AddVertex(p1);
            st.SetUV(new Vector2(1f, 0f)); st.AddVertex(p3);
            st.SetUV(new Vector2(0f, 0f)); st.AddVertex(p2);
        }

        st.GenerateNormals();
        st.Index();
        return st.Commit() ?? new ArrayMesh();
    }

    private MultiMesh CreateMultiMesh(ArrayMesh mesh)
    {
        return new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            InstanceCount = MaxInstances,
            VisibleInstanceCount = 0,
            Mesh = mesh,
            CustomAabb = new Aabb(new Vector3(-5000f, -1000f, -30000f), new Vector3(35000f, 5000f, 60000f))
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PseudoRandom(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
        h ^= h >> 13;
        h *= 0x165667B1u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777215f;
    }
}