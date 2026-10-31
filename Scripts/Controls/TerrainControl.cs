using BCnEncoder.Shared;
using Client.Data;
using Client.Data.ATT;
using Client.Data.MAP;
using Client.Data.OZB;
using Client.Main;
using Client.Main.Content;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class TerrainControl : Node3D
{
    public static TerrainControl Instance { get; private set; }

    public const float SpecialHeight = 1200f;
    public const int BlockSize = 4;
    private const int MAX_LOD_LEVELS = 20;
    private const float LOD_DISTANCE_MULTIPLIER = 3000f;
    private const float CAMERA_MOVE_THRESHOLD = 32f;

    private Camera3D _camera;
    private Node3D tileContainer;
    
    private TerrainAttribute _terrain;
    private TerrainMapping _mapping;
    private Vector3[] _terrainNormal;
    private Texture2D[] _textures;
    private Color[] _backTerrainHeight;
    private Color[] _terrainLightData;
    private Color[] _backTerrainLight;

    [Export] public short WorldIndex;
    public bool IsLoaded { get; private set; } = false;
    public Vector3 Light { get; set; } = new Vector3(0.5f, -0.5f, 0.5f);
    public short worldIndex { get => WorldIndex; set => WorldIndex = value; }

    public Dictionary<int, string> TextureMappingFiles = new Dictionary<int, string>
    {
        { 0, "TileGrass01.ozj" },
        { 1, "TileGrass02.ozj" },
        { 2, "TileGround01.ozj" },
        { 3, "TileGround02.ozj" },
        { 4, "TileGround03.ozj" },
        { 5, "TileWater01.ozj" },
        { 6, "TileWood01.ozj" },
        { 7, "TileRock01.ozj" },
        { 8, "TileRock02.ozj" },
        { 9, "TileRock03.ozj" },
        { 10, "TileRock04.ozj" },
        { 11, "TileRock05.ozj" },
        { 12, "TileRock06.ozj" },
        { 13, "TileRock07.ozj" },
        { 30, "TileGrass01.ozt" },
        { 31, "TileGrass02.ozt" },
        { 32, "TileGrass03.ozt" },
        { 100, "leaf01.ozt" },
        { 101, "leaf02.ozj" },
        { 102, "rain01.ozt" },
        { 103, "rain02.ozt" },
        { 104, "rain03.ozt" }
    };

    private readonly Vector3[] _tempTerrainVertices = new Vector3[4];
    private readonly Vector2[] _terrainTextureCoord = new Vector2[4];
    private readonly Color[] _tempTerrainLights = new Color[4];

    private readonly TerrainBlockCache _blockCache;

    public TerrainControl()
    {
        _blockCache = new TerrainBlockCache(BlockSize, Client.Main.Constants.TERRAIN_SIZE);
    }

    public override async void _Ready()
    {
        Instance = this;
        tileContainer = new Node3D();
        AddChild(tileContainer);

        // _camera = GetViewport().GetCamera3D();
        // if (_camera != null)
        // {
        //     _camera.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(32, 20, 32));
        //     _camera.LookAt(new Vector3(0, 0, 0), Vector3.Up);
        // }

        await LoadTerrainAsync();

        tileContainer.Scale = new Vector3(1, 1, -1);
        RenderTerrain();
        RenderTerrainWireframe();
        IsLoaded = true;
        AddChild(new DebugOverlay());
    }

    public override void _Process(double delta)
    {

    }

    public async Task LoadTerrainAsync()
    {
        string worldFolder = $"World{WorldIndex}";
        string baseDataPath = "F:/Program Files/MuCloneData/Data";
        string fullPathWorldFolder = Path.Combine(baseDataPath, worldFolder);

        if (!System.IO.Directory.Exists(fullPathWorldFolder))
        {
            return;
        }

        var terrainReader = new ATTReader();
        var ozbReader = new OZBReader();
        var mappingReader = new MapReader();

        string attPath = System.IO.Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att");
        if (File.Exists(attPath))
        {
            _terrain = await terrainReader.Load(attPath);
        }
        else
        {
            GD.PrintErr($"ATT file not found: {attPath}");
            return;
        }

        string heightPath = System.IO.Path.Combine(fullPathWorldFolder, "TerrainHeight.OZB");
        if (File.Exists(heightPath))
        {
            var heightData = await ozbReader.Load(heightPath);
            _backTerrainHeight = heightData.Data.Select(c => new Color(c.R, c.R, c.R)).ToArray();
        }
        else
        {
            return;
        }

        string mapPath = System.IO.Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map");
        if (File.Exists(mapPath))
        {
            _mapping = await mappingReader.Load(mapPath);
        }
        else
        {
            return;
        }

        var textureMapFiles = new string[256];

        foreach (var kvp in TextureMappingFiles)
        {
            textureMapFiles[kvp.Key] = Path.Combine(fullPathWorldFolder, kvp.Value);
        }

        for (int i = 1; i <= 36; i++)
        {
            textureMapFiles[13 + i] = Path.Combine(fullPathWorldFolder, $"ExtTile{i:00}.ozj");
        }

        _textures = new Texture2D[textureMapFiles.Length];

        for (int t = 0; t < textureMapFiles.Length; t++)
        {
            string path = textureMapFiles[t];
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            _textures[t] = await TextureLoader.Instance.PrepareAndGetTexture(path);
        }

        string textureLightPath = System.IO.Path.Combine(fullPathWorldFolder, "TerrainLight.OZB");

        if (System.IO.File.Exists(textureLightPath))
        {
            var lightData = await ozbReader.Load(textureLightPath);
            _terrainLightData = lightData.Data.Select(c => new Color(c.R / 255f, c.G / 255f, c.B / 255f, 1f)).ToArray();
        }
        else
        {
            _terrainLightData = new Color[Client.Main.Constants.TERRAIN_SIZE * Client.Main.Constants.TERRAIN_SIZE];
            for (int i = 0; i < _terrainLightData.Length; i++)
                _terrainLightData[i] = Colors.White;
        }

        if (_backTerrainHeight != null && _backTerrainHeight.Length > 0)
            CreateTerrainNormal();
        if (_terrainLightData != null && _terrainLightData.Length > 0)
            CreateTerrainLight();
    }

    private class TerrainBlock
    {
        public float MinY;
        public float MaxY;
        public int LODLevel;
        public Vector3 Center;
        public bool IsVisible;
        public int Xi;
        public int Zi;
    }

    private class TerrainBlockCache
    {
        private readonly TerrainBlock[,] _blocks;
        private readonly int _blockSize;
        private readonly int _gridSize;
        
        public TerrainBlockCache(int blockSize, int terrainSize)
        {
            _blockSize = blockSize;
            _gridSize = terrainSize / blockSize;
            _blocks = new TerrainBlock[_gridSize, _gridSize];

            for (int z = 0; z < _gridSize; z++)
            {
                for (int x = 0; x < _gridSize; x++)
                {
                    _blocks[z, x] = new TerrainBlock
                    {
                        Xi = x * blockSize,
                        Zi = z * blockSize
                    };
                }
            }
        }
        
        public TerrainBlock GetBlock(int x, int z) => _blocks[z, x];

        public void PrintBlocks()
        {
            for (int z = 0; z < _gridSize; z++)
            {
                for (int x = 0; x < _gridSize; x++)
                {
                    TerrainBlock block = _blocks[z, x];
                    GD.Print($"Block ({x}, {z}): " +
                            $"Center = {block.Center}, " +
                            $"MinZ = {block.MinY}, " +
                            $"MaxZ = {block.MaxY}, " +
                            $"LOD = {block.LODLevel}, " +
                            $"IsVisible = {block.IsVisible}, " +
                            $"Xi = {block.Xi}, " +
                            $"Yi = {block.Zi}"
                            );
                }
            }
        }
    }

    private void CreateTerrainNormal()
{
    int sizeX = Client.Main.Constants.TERRAIN_SIZE;
    int sizeZ = Client.Main.Constants.TERRAIN_SIZE;
    int maxIndex = _backTerrainHeight.Length - 1;

    _terrainNormal = new Vector3[sizeX * sizeZ];

    for (int y = 0; y < sizeZ; y++)
    {
        for (int x = 0; x < sizeX; x++)
        {
            int index = GetTerrainIndex(x, y);
            if (index > maxIndex) continue;

            // Παίρνουμε τα ύψη με ασφάλεια (repeat wrapping)
            float h1 = _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y)].R * 1.5f;
            float h2 = _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y + 1)].R * 1.5f;
            float h3 = _backTerrainHeight[GetTerrainIndexRepeat(x, y + 1)].R * 1.5f;
            float h4 = _backTerrainHeight[GetTerrainIndexRepeat(x, y)].R * 1.5f;

            // Αντιστοίχιση αξόνων για το Godot (X = x, Y = ύψος, Z = y)
            float scale = Client.Main.Constants.TERRAIN_SCALE;
            Vector3 v1 = new Vector3((x + 1) * scale, h1, y * scale);
            Vector3 v2 = new Vector3((x + 1) * scale, h2, (y + 1) * scale);
            Vector3 v3 = new Vector3(x * scale, h3, (y + 1) * scale);
            Vector3 v4 = new Vector3(x * scale, h4, y * scale);

            // Υπολογισμός face normals (όπως ακριβώς στο MonoGame MathUtils.FaceNormalize)
            Vector3 n1 = FaceNormalize(v1, v2, v3);
            Vector3 n2 = FaceNormalize(v3, v4, v1);

            _terrainNormal[index] = n1 + n2;

            Vector3 finalNormal = n1 + n2;
if (finalNormal.Y < 0) finalNormal = -finalNormal; // Διασφάλιση ότι κοιτάζει προς τα πάνω
_terrainNormal[index] = finalNormal.Normalized();
        }
    }

    for (int i = 0; i < _terrainNormal.Length; i++)
    {
        if (_terrainNormal[i].LengthSquared() > 0.0001f)
            _terrainNormal[i] = _terrainNormal[i].Normalized();
        else
            _terrainNormal[i] = Vector3.Up;
    }
}

// Βοηθητική συνάρτηση FaceNormalize αντίστοιχη του MonoGame MathUtils
private static Vector3 FaceNormalize(Vector3 v1, Vector3 v2, Vector3 v3)
{
    Vector3 n = (v2 - v1).Cross(v3 - v1);
    return n.LengthSquared() < 0.000001f ? Vector3.Zero : n.Normalized();
}

    private void CreateTerrainLight()
{
    int sizeX = Client.Main.Constants.TERRAIN_SIZE;
    int sizeZ = Client.Main.Constants.TERRAIN_SIZE;

    int maxIndex = Math.Min(_terrainLightData.Length, _terrainNormal.Length) - 1;
    _backTerrainLight = new Color[_terrainNormal.Length];

    // Η ακριβής κατεύθυνση φωτός του MU Online από τον C++ κώδικα:
    // Vector(0.5f, -0.5f, 0.5f) -> Προσοχή στο coordinate system του Godot
    Vector3 muLightDir = new Vector3(0.5f, 0.5f, -0.5f).Normalized(); 

    for (int z = 0; z < sizeZ; z++)
    {
        for (int x = 0; x < sizeX; x++)
        {
            int index = GetTerrainIndex(x, z);
            if (index > maxIndex) continue;

            Vector3 normal = _terrainNormal[index];
            int lightIndex = Math.Min(index, _terrainLightData.Length - 1);

            // Η ακριβής φόρμουλα του MU C++ κώδικα: DotProduct + 0.5f, clamped στο [0, 1]
            float luminosity = Mathf.Clamp(normal.Dot(muLightDir) + 0.5f, 0f, 1f);

            Color baseLight = _terrainLightData[lightIndex];

            // Πολλαπλασιασμός ακριβώς όπως στο BackTerrainLight[Index] = TerrainLight[Index] * Luminosity
            _backTerrainLight[index] = new Color(
                baseLight.R * luminosity,
                baseLight.G * luminosity,
                baseLight.B * luminosity,
                1f
            );
        }
    }
}

    private void RenderTerrain()
    {
        if (_backTerrainHeight == null)
            return;

        var surfaceTools = new Dictionary<int, SurfaceTool>();
        var alphaSurfaceTools = new Dictionary<int, SurfaceTool>();
        var vertexCounts = new Dictionary<int, int>();
        var alphaVertexCounts = new Dictionary<int, int>();

        int blocksPerAxis = Client.Main.Constants.TERRAIN_SIZE / BlockSize;

        for (int blockZ = 0; blockZ < blocksPerAxis; blockZ++)
        {
            for (int blockX = 0; blockX < blocksPerAxis; blockX++)
            {
                var block = _blockCache.GetBlock(blockX, blockZ);
                float xStart = block.Xi * Client.Main.Constants.TERRAIN_SCALE;
                float zStart = block.Zi * Client.Main.Constants.TERRAIN_SCALE;

                RenderTerrainBlockIntoBatch(
                    xStart / Client.Main.Constants.TERRAIN_SCALE,
                    zStart / Client.Main.Constants.TERRAIN_SCALE,
                    block.Xi,
                    block.Zi,
                    surfaceTools,
                    alphaSurfaceTools,
                    vertexCounts,
                    alphaVertexCounts
                );
            }
        }

        CommitBatchMeshes(surfaceTools, useAlpha: false);
        CommitBatchMeshes(alphaSurfaceTools, useAlpha: true);
    }

    private void RenderTerrainBlockIntoBatch(float xf, float zf, int xi, int zi, Dictionary<int, SurfaceTool> stMap, Dictionary<int, SurfaceTool> alphaStMap, Dictionary<int, int> vcMap, Dictionary<int, int> alphaVcMap)
    {
        for (int i = 0; i < BlockSize; i++)
        {
            for (int j = 0; j < BlockSize; j++)
            {
                RenderTerrainTileIntoBatch(xf + j, zf + i, xi + j, zi + i, 1f, stMap, alphaStMap, vcMap, alphaVcMap);
            }
        }
    }

    private void RenderTerrainTileIntoBatch(float xf, float zf, int xi, int zi, float lodf, Dictionary<int, SurfaceTool> stMap, Dictionary<int, SurfaceTool> alphaStMap, Dictionary<int, int> vcMap, Dictionary<int, int> alphaVcMap)
    {
        int idx1 = GetTerrainIndex(xi, zi);

        int textureIndexMain = _mapping.Layer2[idx1];
        int textureIndexOverlay = _mapping.Layer1[idx1];

        if (textureIndexMain == 255 && textureIndexOverlay == 255)
            return;

        if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.NoGround))
            return;

        int idx2 = GetTerrainIndex(xi + 1, zi);
        int idx3 = GetTerrainIndex(xi + 1, zi + 1);
        int idx4 = GetTerrainIndex(xi, zi + 1);

        PrepareTileVertices(xi, zi, xf, zf, idx1, idx2, idx3, idx4, lodf);
        PrepareTileLights(idx1, idx2, idx3, idx4);

        byte alpha1 = idx1 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx1];
        byte alpha2 = idx2 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx2];
        byte alpha3 = idx3 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx3];
        byte alpha4 = idx4 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx4];

        bool isOpaque = alpha1 >= 255 && alpha2 >= 255 && alpha3 >= 255 && alpha4 >= 255;
        bool hasAlpha = alpha1 > 0 || alpha2 > 0 || alpha3 > 0 || alpha4 > 0;

        if (isOpaque)
        {
            AppendDataToSurface(_mapping.Layer2[idx1], xf, zf, lodf, useAlpha: false, stMap, vcMap);
        }
        else
        {
            AppendDataToSurface(_mapping.Layer1[idx1], xf, zf, lodf, useAlpha: false, stMap, vcMap);
        }

        if (hasAlpha && !isOpaque)
        {
            ApplyAlphaToLights(alpha1, alpha2, alpha3, alpha4);
            AppendDataToSurface(_mapping.Layer2[idx1], xf, zf, lodf, useAlpha: true, alphaStMap, alphaVcMap);
        }
    }

    private void AppendDataToSurface(int textureIndex, float xf, float zf, float lodScale, bool useAlpha, Dictionary<int, SurfaceTool> dictionary, Dictionary<int, int> vertexCounts)
    {
        if (textureIndex == 255 || textureIndex < 0 || textureIndex >= _textures.Length || _textures[textureIndex] == null)
            return;

        Texture2D texture = _textures[textureIndex];

        float baseWidth = 64f / texture.GetSize().X;
        float baseHeight = 64f / texture.GetSize().Y;
        float suf = xf * baseWidth;
        float svf = zf * baseHeight;
        float uvWidth = baseWidth * lodScale;
        float uvHeight = baseHeight * lodScale;

        _terrainTextureCoord[0] = new Vector2(suf, svf);
        _terrainTextureCoord[1] = new Vector2(suf + uvWidth, svf);
        _terrainTextureCoord[2] = new Vector2(suf + uvWidth, svf + uvHeight);
        _terrainTextureCoord[3] = new Vector2(suf, svf + uvHeight);

        Vector3[] normals = new Vector3[4]
        {
            _terrainNormal[GetTerrainIndexRepeat((int)xf, (int)zf)],
            _terrainNormal[GetTerrainIndexRepeat((int)xf + 1, (int)zf)],
            _terrainNormal[GetTerrainIndexRepeat((int)xf + 1, (int)zf + 1)],
            _terrainNormal[GetTerrainIndexRepeat((int)xf, (int)zf + 1)]
        };

        if (!dictionary.TryGetValue(textureIndex, out var st))
        {
            st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            dictionary[textureIndex] = st;
            vertexCounts[textureIndex] = 0;
        }

        int baseIndex = vertexCounts[textureIndex];

        for (int i = 0; i < 4; i++)
        {
            st.SetUV(_terrainTextureCoord[i]);
            st.SetColor(_tempTerrainLights[i]);
            st.SetNormal(normals[i]); // Χρησιμοποιούμε το Vector3.Up για να αποφύγουμε προβλήματα με τα normals
            st.AddVertex(_tempTerrainVertices[i]);
        }

        st.AddIndex(baseIndex + 0);
        st.AddIndex(baseIndex + 1);
        st.AddIndex(baseIndex + 2);
        
        st.AddIndex(baseIndex + 0);
        st.AddIndex(baseIndex + 2);
        st.AddIndex(baseIndex + 3);

        vertexCounts[textureIndex] += 4;
    }

    private void CommitBatchMeshes(Dictionary<int, SurfaceTool> dictionary, bool useAlpha)
    {
        foreach (var kvp in dictionary)
        {
            int textureIndex = kvp.Key;
            SurfaceTool st = kvp.Value;

            Mesh mesh = st.Commit();
            Material material;

            if (textureIndex == 5)
            {
                ShaderMaterial waterMat = new ShaderMaterial();
                waterMat.Shader = GD.Load<Shader>("res://Shaders/WaterShader.gdshader");
                waterMat.SetShaderParameter("_MainTex", _textures[textureIndex]);
                waterMat.SetShaderParameter("_DistortionFrequency", 5.0f);
                waterMat.SetShaderParameter("_DistortionAmplitude", 0.02f);
                waterMat.SetShaderParameter("_WaterSpeed", 0.1f);
                waterMat.SetShaderParameter("_ScrollSpeed", new Vector2(0.1f, 0.1f));
              
                // Ιδανικό χρώμα νερού MU Online για να ταιριάζει απόλυτα
                waterMat.SetShaderParameter("_WaterTint", new Color(0.84f, 0.97f, 1.08f));
                
                material = waterMat;
            }
            else
            {
                var mat = new StandardMaterial3D();
                mat.AlbedoTexture = _textures[textureIndex];
                mat.VertexColorUseAsAlbedo = true;

                mat.VertexColorIsSrgb = true;
                mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
                mat.Roughness = 1.0f;

                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
                mat.Transparency = useAlpha ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
                mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Απενεργοποίηση culling για να εμφανίζονται και οι δύο πλευρές
                material = mat;
            }

            MeshInstance3D tile = new MeshInstance3D
            {
                Mesh = mesh,
                MaterialOverride = material
            };

            if (useAlpha && material is BaseMaterial3D baseMat)
            {
                baseMat.RenderPriority = 1; // Σχεδιάζεται πάνω από τα opaque
            }

            tileContainer.AddChild(tile);
        }
    }

    private void PrepareTileVertices(int xi, int zi, float xf, float zf, int idx1, int idx2, int idx3, int idx4, float lodf)
    {
        float terrainHeight1 = idx1 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx1].R * 1.5f;
        float terrainHeight2 = idx2 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx2].R * 1.5f;
        float terrainHeight3 = idx3 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx3].R * 1.5f;
        float terrainHeight4 = idx4 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx4].R * 1.5f;

        _tempTerrainVertices[0] = new Vector3(xf * Client.Main.Constants.TERRAIN_SCALE, terrainHeight1, zf * Client.Main.Constants.TERRAIN_SCALE);
        _tempTerrainVertices[1] = new Vector3((xf + 1) * Client.Main.Constants.TERRAIN_SCALE, terrainHeight2, zf * Client.Main.Constants.TERRAIN_SCALE);
        _tempTerrainVertices[2] = new Vector3((xf + 1) * Client.Main.Constants.TERRAIN_SCALE, terrainHeight3, (zf + 1) * Client.Main.Constants.TERRAIN_SCALE);
        _tempTerrainVertices[3] = new Vector3(xf * Client.Main.Constants.TERRAIN_SCALE, terrainHeight4, (zf + 1) * Client.Main.Constants.TERRAIN_SCALE);

        if (idx1 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx1].HasFlag(TWFlags.Height))
            _tempTerrainVertices[0].Y += SpecialHeight;
        if (idx2 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx2].HasFlag(TWFlags.Height))
            _tempTerrainVertices[1].Y += SpecialHeight;
        if (idx3 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx3].HasFlag(TWFlags.Height))
            _tempTerrainVertices[2].Y += SpecialHeight;
        if (idx4 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx4].HasFlag(TWFlags.Height))
            _tempTerrainVertices[3].Y += SpecialHeight;
    }

    private void PrepareTileLights(int idx1, int idx2, int idx3, int idx4)
    {
        _tempTerrainLights[0] = idx1 < _backTerrainLight.Length ? _backTerrainLight[idx1] : Colors.White;
        _tempTerrainLights[1] = idx2 < _backTerrainLight.Length ? _backTerrainLight[idx2] : Colors.White;
        _tempTerrainLights[2] = idx3 < _backTerrainLight.Length ? _backTerrainLight[idx3] : Colors.White;
        _tempTerrainLights[3] = idx4 < _backTerrainLight.Length ? _backTerrainLight[idx4] : Colors.White;
    }

    private void ApplyAlphaToLights(byte alpha1, byte alpha2, byte alpha3, byte alpha4)
    {
        _tempTerrainLights[0].A = alpha1 / 255f;
        _tempTerrainLights[1].A = alpha2 / 255f;
        _tempTerrainLights[2].A = alpha3 / 255f;
        _tempTerrainLights[3].A = alpha4 / 255f;
    }

    private static int GetTerrainIndex(int x, int y) => y * Client.Main.Constants.TERRAIN_SIZE + x;
    
    private static int GetTerrainIndexRepeat(int x, int y) =>
        ((y & Client.Main.Constants.TERRAIN_SIZE_MASK) * Client.Main.Constants.TERRAIN_SIZE) + (x & Client.Main.Constants.TERRAIN_SIZE_MASK);
    
    public TWFlags RequestTerraingFlag(int x, int y) => _terrain.TerrainWall[GetTerrainIndex(x, y)];

    public float GetHeight(float worldX, float worldZ)
    {
        if (_backTerrainHeight == null) 
            return 0f;

        int tx = Mathf.Clamp(
            Mathf.RoundToInt(worldX / Client.Main.Constants.TERRAIN_SCALE),
            0,
            Client.Main.Constants.TERRAIN_SIZE - 1
        );

        int tz = Mathf.Clamp(
            Mathf.RoundToInt(worldZ / Client.Main.Constants.TERRAIN_SCALE),
            0,
            Client.Main.Constants.TERRAIN_SIZE - 1
        );

        int index = GetTerrainIndex(tx, tz);

        if (index < 0 || index >= _backTerrainHeight.Length)
            return 0f;

        return _backTerrainHeight[index].R * 1.5f;
    }

    private void RenderTerrainWireframe()
{
    if (_backTerrainHeight == null)
        return;

    var wireSt = new SurfaceTool();
    wireSt.Begin(Mesh.PrimitiveType.Lines);

    int blocksPerAxis = Client.Main.Constants.TERRAIN_SIZE / BlockSize;

    for (int blockZ = 0; blockZ < blocksPerAxis; blockZ++)
    {
        for (int blockX = 0; blockX < blocksPerAxis; blockX++)
        {
            var block = _blockCache.GetBlock(blockX, blockZ);
            float xStart = block.Xi;
            float zStart = block.Zi;

            for (int i = 0; i < BlockSize; i++)
            {
                for (int j = 0; j < BlockSize; j++)
                {
                    float xf = xStart + j;
                    float zf = zStart + i;
                    int xi = (int)xf;
                    int zi = (int)zf;

                    int idx1 = GetTerrainIndex(xi, zi);
                    int idx2 = GetTerrainIndex(xi + 1, zi);
                    int idx3 = GetTerrainIndex(xi + 1, zi + 1);
                    int idx4 = GetTerrainIndex(xi, zi + 1);

                    // Παίρνουμε τις κορυφές (ίδιες με το PrepareTileVertices)
                    float h1 = idx1 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx1].R * 1.5f;
                    float h2 = idx2 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx2].R * 1.5f;
                    float h3 = idx3 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx3].R * 1.5f;
                    float h4 = idx4 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx4].R * 1.5f;

                    Vector3 v1 = new Vector3(xf * Client.Main.Constants.TERRAIN_SCALE, h1, zf * Client.Main.Constants.TERRAIN_SCALE);
                    Vector3 v2 = new Vector3((xf + 1) * Client.Main.Constants.TERRAIN_SCALE, h2, zf * Client.Main.Constants.TERRAIN_SCALE);
                    Vector3 v3 = new Vector3((xf + 1) * Client.Main.Constants.TERRAIN_SCALE, h3, (zf + 1) * Client.Main.Constants.TERRAIN_SCALE);
                    Vector3 v4 = new Vector3(xf * Client.Main.Constants.TERRAIN_SCALE, h4, (zf + 1) * Client.Main.Constants.TERRAIN_SCALE);

                    // Προσθήκη γραμμών τετραγώνου (v1->v2, v2->v3, v3->v4, v4->v1)
                    DrawLine(wireSt, v1, v2);
                    DrawLine(wireSt, v2, v3);
                    DrawLine(wireSt, v3, v4);
                    DrawLine(wireSt, v4, v1);
                }
            }
        }
    }

    Mesh mesh = wireSt.Commit();
    var mat = new StandardMaterial3D();
    mat.AlbedoColor = Colors.WebMaroon;
    mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;

    MeshInstance3D wireInstance = new MeshInstance3D
    {
        Mesh = mesh,
        MaterialOverride = mat
    };

    tileContainer.AddChild(wireInstance);
}

private void DrawLine(SurfaceTool st, Vector3 a, Vector3 b)
{
    st.SetColor(Colors.White);
    st.AddVertex(a);
    st.SetColor(Colors.White);
    st.AddVertex(b);
}
public float GetHeightInterpolated(float worldX, float worldZ)
{
    if (_backTerrainHeight == null)
        return 0f;

    float scale = Client.Main.Constants.TERRAIN_SCALE;
    float x = worldX / scale;
    float z = worldZ / scale;

    int size = Client.Main.Constants.TERRAIN_SIZE;
    x = Mathf.Clamp(x, 0f, size - 1.001f);
    z = Mathf.Clamp(z, 0f, size - 1.001f);

    int x0 = (int)MathF.Floor(x);
    int z0 = (int)MathF.Floor(z);
    int x1 = Math.Min(x0 + 1, size - 1);
    int z1 = Math.Min(z0 + 1, size - 1);

    float tx = x - x0;
    float tz = z - z0;

    float h00 = GetHeightRaw(x0, z0);
    float h10 = GetHeightRaw(x1, z0);
    float h11 = GetHeightRaw(x1, z1);
    float h01 = GetHeightRaw(x0, z1);

    float h0 = Mathf.Lerp(h00, h10, tx);
    float h1 = Mathf.Lerp(h01, h11, tx);
    return Mathf.Lerp(h0, h1, tz);
}

private float GetHeightRaw(int x, int z)
{
    int index = GetTerrainIndex(x, z);
    if (_backTerrainHeight == null || index < 0 || index >= _backTerrainHeight.Length)
        return 0f;

    return _backTerrainHeight[index].R * 1.5f;
}

public byte GetBaseTextureIndexAt(int x, int y)
{
    x = Math.Clamp(x, 0, Client.Main.Constants.TERRAIN_SIZE - 1);
    y = Math.Clamp(y, 0, Client.Main.Constants.TERRAIN_SIZE - 1);
    int idx = GetTerrainIndex(x, y);

    byte layer1 = (_mapping.Layer1 != null && idx < _mapping.Layer1.Length) ? _mapping.Layer1[idx] : (byte)0;
    byte layer2 = (_mapping.Layer2 != null && idx < _mapping.Layer2.Length) ? _mapping.Layer2[idx] : layer1;
    byte alpha = (_mapping.Alpha != null && idx < _mapping.Alpha.Length) ? _mapping.Alpha[idx] : (byte)0;

    // Αν το Layer2 καλύπτει πλήρως το tile (alpha >= 253) και έχει έγκυρο texture, επιστρέφουμε το Layer2 (π.χ. πέτρα πόλης)
    if (alpha >= 253 && layer2 != 255)
        return layer2;

    return layer1;
}

public Color GetTerrainLightColor(int x, int y)
{
    int idx = GetTerrainIndex(x, y);
    if (_backTerrainLight != null && idx < _backTerrainLight.Length)
        return _backTerrainLight[idx];
    return Colors.White;
}

public bool IsPureGrassTile(int x, int y, HashSet<byte> grassTextureIndices)
{
    int size = Client.Main.Constants.TERRAIN_SIZE;
    if (x < 0 || y < 0 || x >= size - 1 || y >= size - 1)
        return false;

    int idx1 = GetTerrainIndex(x, y);
    int idx2 = GetTerrainIndex(x + 1, y);
    int idx3 = GetTerrainIndex(x + 1, y + 1);
    int idx4 = GetTerrainIndex(x, y + 1);

    // 1. Έλεγχος ATT Flags: Όχι σε SafeZone, NoGround, Water ή πάνω σε εμπόδια/βράχους (NoMove)
    if (_terrain != null && _terrain.TerrainWall != null && idx1 < _terrain.TerrainWall.Length)
    {
        var flags = _terrain.TerrainWall[idx1];
        if (flags.HasFlag(TWFlags.NoGround) ||
            flags.HasFlag(TWFlags.SafeZone) ||
            flags.HasFlag(TWFlags.Water) ||
            flags.HasFlag(TWFlags.NoMove))
        {
            return false;
        }
    }

    // 2. Το βασικό Layer1 πρέπει να είναι στη λίστα με τα επιτρεπτά grass textures (π.χ. 0)
    byte layer1 = (_mapping.Layer1 != null && idx1 < _mapping.Layer1.Length) ? _mapping.Layer1[idx1] : (byte)255;
    if (!grassTextureIndices.Contains(layer1))
        return false;

    // 3. Έλεγχος Layer2 & Alpha και στις 4 γωνίες του tile!
    // Αν υπάρχει overlay από άλλη υφή (π.χ. χώμα μονοπατιού, πέτρα, νερό) σε ΟΠΟΙΑΔΗΠΟΤΕ γωνία του tile, 
    // απαγορεύουμε το γρασίδι για να μένουν πεντακάθαρα τα μονοπάτια και οι όχθες!
    if (_mapping.Layer2 != null && _mapping.Alpha != null)
    {
        int[] corners = { idx1, idx2, idx3, idx4 };
        foreach (int cIdx in corners)
        {
            if (cIdx < _mapping.Alpha.Length && cIdx < _mapping.Layer2.Length)
            {
                byte alpha = _mapping.Alpha[cIdx];
                byte layer2 = _mapping.Layer2[cIdx];

                // Αν η γωνία έχει έστω και λίγο blend (alpha > 10) με άλλη υφή (που δεν είναι γρασίδι), το απορρίπτουμε
                if (alpha > 10 && layer2 != 255 && !grassTextureIndices.Contains(layer2))
                    return false;
            }
        }
    }

    return true;
}

}