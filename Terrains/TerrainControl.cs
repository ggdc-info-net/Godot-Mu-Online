using BCnEncoder.Shared;
using Client.Data;
using Client.Data.ATT;
using Client.Data.MAP;
using Client.Data.OZB;
using Client.Main;
using Client.Main.Content;
using Godot;
using SixLabors.ImageSharp.PixelFormats;
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
    private Vector2 _lastCameraPosition;
    private Node3D _tileRoot;

    //-----------------------------
    private MeshInstance3D[,] _tiles;
    private Material _terrainMaterial;
    private Node3D tileContainer;
    private HashSet<(int, int)> _renderedBlocks = new HashSet<(int, int)>();
    //-----------------------------
    private TerrainAttribute _terrain;
    private TerrainMapping _mapping;
    private float[] _terrainHeight;
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
        { 103,  "rain02.ozt" },
        { 104,  "rain03.ozt" }
    };

    private readonly Vector2[] _terrainTextureCoord;
    private readonly Vector3[] _tempTerrainVertex;
    private readonly Color[] _tempTerrainLights;
    private ColorRgba32[] colors;

    private readonly TerrainBlockCache _blockCache;
    private readonly Queue<TerrainBlock> _visibleBlocks = new Queue<TerrainBlock>(64);

    public TerrainControl()
    {
        _blockCache = new TerrainBlockCache(BlockSize, Client.Main.Constants.TERRAIN_SIZE);
        _terrainTextureCoord = new Vector2[4];
        _tempTerrainVertex = new Vector3[4];
        _tempTerrainLights = new Color[4];
    }

    public override async void _Ready()
    {
        Instance = this;
        tileContainer = new Node3D();
        AddChild(tileContainer);

        _camera = GetViewport().GetCamera3D();
        if (_camera != null)
        {
            _camera.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(32, 20, 32));
            _camera.LookAt(new Vector3(0, 0, 0), Vector3.Up);
        }

        await LoadTerrainAsync();
        //CallDeferred(nameof(RenderSingleTestTile));

        tileContainer.Scale = new Vector3(1, 1, -1);
        RenderTerrain();
        IsLoaded = true;


    }

    public override void _Process(double delta)
    {

    }

    public async Task LoadTerrainAsync()
    {
        //GD.Print("Load Starts...");

        string worldFolder = $"World{WorldIndex}";
        string baseDataPath = "F:/Program Files/MuCloneData/Data";
        string fullPathWorldFolder = Path.Combine(baseDataPath, worldFolder);

        //GD.Print(worldFolder);

        if (!System.IO.Directory.Exists(fullPathWorldFolder))
        {
            //GD.Print("Error: World Folder does not exist.");
            return;
        }

        //Readers
        var terrainReader = new ATTReader();
        var ozbReader = new OZBReader();
        var mappingReader = new MapReader();

        //Load ATT terrain
        string attPath = System.IO.Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att");
        if (File.Exists(attPath))
        {
            _terrain = await terrainReader.Load(attPath);
            //GD.Print("✔ Terrain ATT loaded");
        }
        else
        {
            GD.PrintErr($"ATT file not found: {attPath}");
            return;
        }

        //Load terrain Height
        string heightPath = System.IO.Path.Combine(fullPathWorldFolder, "TerrainHeight.OZB");
        if (File.Exists(heightPath))
        {
            var heightData = await ozbReader.Load(heightPath);

            _backTerrainHeight = heightData.Data.Select(c => new Color(c.R, c.R, c.R)).ToArray();
            //GD.Print("✔ Terrain Height loaded");
            //GD.Print($"Height min: {_backTerrainHeight.Min(c => c.R)}, max: {_backTerrainHeight.Max(c => c.R)}");

            // foreach(var Color in _backTerrainHeight)
            // {
            // 	GD.Print($"Color: R={Color.R}, G={Color.G}, B={Color.B}");
            // }
        }
        else
        {
            //GD.PrintErr($"TerrainHeight.OZB not found: {heightPath}");
            return;
        }

        //Load terrain mapping
        string mapPath = System.IO.Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map");
        if (File.Exists(mapPath))
        {
            _mapping = await mappingReader.Load(mapPath);
            //GD.Print("✔ Terrain Mapping loaded");
        }
        else
        {
            //GD.PrintErr($"Map file not found: {mapPath}");
            return;
        }


        //Load textures
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

        //GD.Print(_textures.Length);

        for (int t = 0; t < textureMapFiles.Length; t++)
        {
            string path = textureMapFiles[t];
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            _textures[t] = await TextureLoader.Instance.PrepareAndGetTexture(path);
            //GD.Print($"✔ Texture loaded: {path}");
        }


        //Load terrain light
        string textureLightPath = System.IO.Path.Combine(fullPathWorldFolder, "TerrainLight.OZB");

        if (System.IO.File.Exists(textureLightPath))
        {
            var lightData = await ozbReader.Load(textureLightPath);
            _terrainLightData = lightData.Data.Select(c => new Color(c.R / 255f, c.G / 255f, c.B / 255f, 1f)).ToArray();
            // foreach(var Color in _terrainLightData)
            // {
            // 	GD.Print($"Color: R={Color.R}, G={Color.G}, B={Color.B}");
            // }
        }
        else
        {
            _terrainLightData = new Color[Client.Main.Constants.TERRAIN_SIZE * Client.Main.Constants.TERRAIN_SIZE];
            for (int i = 0; i < _terrainLightData.Length; i++)
                _terrainLightData[i] = Colors.White;
        }
        //GD.Print("Terrain light loaded.");

        // Generate normals and lighting
        if (_backTerrainHeight != null && _backTerrainHeight.Length > 0)
            CreateTerrainNormal();
        if (_terrainLightData != null && _terrainLightData.Length > 0)
            CreateTerrainLight();

        //GD.Print("Terrain data ready!");

    }

    private class TerrainBlock
    {
        //public Bounds Bounds;
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
        //GD.Print("CreateTerrainNormal");

        int sizeX = Client.Main.Constants.TERRAIN_SIZE; // horizontal
        int sizeZ = Client.Main.Constants.TERRAIN_SIZE; // vertical

        int maxIndex = _backTerrainHeight.Length - 1;

        _terrainNormal = new Vector3[Client.Main.Constants.TERRAIN_SIZE * Client.Main.Constants.TERRAIN_SIZE];

        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int index = GetTerrainIndex(x, z);
                if (index > maxIndex) continue; // safety

                // Compute neighboring indices safely
                int x1 = Math.Min(x + 1, sizeX - 1);
                int z1 = Math.Min(z + 1, sizeZ - 1);

                int i1 = Math.Min(GetTerrainIndex(x1, z), maxIndex);
                int i2 = Math.Min(GetTerrainIndex(x1, z1), maxIndex);
                int i3 = Math.Min(GetTerrainIndex(x, z1), maxIndex);
                int i4 = Math.Min(index, maxIndex);

                Vector3 v1 = new Vector3(x1 * Client.Main.Constants.TERRAIN_SCALE, _backTerrainHeight[i1].R, z * Client.Main.Constants.TERRAIN_SCALE);
                Vector3 v2 = new Vector3(x1 * Client.Main.Constants.TERRAIN_SCALE, _backTerrainHeight[i2].R, z1 * Client.Main.Constants.TERRAIN_SCALE);
                Vector3 v3 = new Vector3(x * Client.Main.Constants.TERRAIN_SCALE, _backTerrainHeight[i3].R, z1 * Client.Main.Constants.TERRAIN_SCALE);
                Vector3 v4 = new Vector3(x * Client.Main.Constants.TERRAIN_SCALE, _backTerrainHeight[i4].R, z * Client.Main.Constants.TERRAIN_SCALE);

                Vector3 faceNormal1 = (v2 - v1).Cross(v3 - v1).Normalized();
                Vector3 faceNormal2 = (v4 - v3).Cross(v1 - v3).Normalized();

                _terrainNormal[index] = faceNormal1 + faceNormal2;
            }
        }

        // Normalize all normals
        for (int i = 0; i < _terrainNormal.Length; i++)
            _terrainNormal[i] = _terrainNormal[i].Normalized();

        //GD.Print("✅ CreateTerrainNormal has been initialized!");
    }


    private void CreateTerrainLight()
    {
        //GD.Print("CreateTerrainLight");

        int sizeX = Client.Main.Constants.TERRAIN_SIZE;
        int sizeZ = Client.Main.Constants.TERRAIN_SIZE;

        int maxIndex = Math.Min(_terrainLightData.Length, _terrainNormal.Length) - 1;
        _backTerrainLight = new Color[_terrainNormal.Length];

        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int index = GetTerrainIndex(x, z);
                if (index > maxIndex) continue; // safety

                Vector3 normal = _terrainNormal[index];

                // Clamp light data index
                int lightIndex = Math.Min(index, _terrainLightData.Length - 1);

                float luminosity = Mathf.Clamp(normal.Dot(Light) + 0.5f, 0f, 1f);

                _backTerrainLight[index] = new Color(
                    _terrainLightData[lightIndex].R * luminosity,
                    _terrainLightData[lightIndex].G * luminosity,
                    _terrainLightData[lightIndex].B * luminosity
                );
            }
        }

        //GD.Print("✅ CreateTerrainLight has been initialized!");
    }


    private void RenderTerrain()
    {
        if (_backTerrainHeight == null)
            return;

        // Loop through all terrain blocks
        int blocksPerAxis = Client.Main.Constants.TERRAIN_SIZE / BlockSize;

        for (int blockZ = 0; blockZ < blocksPerAxis; blockZ++)
        {
            for (int blockX = 0; blockX < blocksPerAxis; blockX++)
            {
                var block = _blockCache.GetBlock(blockX, blockZ);
                float xStart = block.Xi * Client.Main.Constants.TERRAIN_SCALE;
                float zStart = block.Zi * Client.Main.Constants.TERRAIN_SCALE;

                // Render the block fully (ignore visibility/frustum culling)
                RenderTerrainBlock(
                    xStart / Client.Main.Constants.TERRAIN_SCALE,
                    zStart / Client.Main.Constants.TERRAIN_SCALE,
                    block.Xi,
                    block.Zi

                );
            }
        }
    }

    private void RenderTerrainBlock(float xf, float zf, int xi, int zi)
    {
        for (int i = 0; i < BlockSize; i++)
        {
            for (int j = 0; j < BlockSize; j++)
            {
                RenderTerrainTile(
                    xf + j,         // world X offset
                    zf + i,         // world Z offset
                    xi + j,         // terrain grid X
                    zi + i,         // terrain grid Z
                    1f,             // scale factor
                    1               // step in grid
                );
            }
        }
    }


    private (Vector3[], int[]) PrepareTileVertices(int xi, int zi, float xf, float zf, int idx1, int idx2, int idx3, int idx4, float lodf)
    {
        float terrainHeight1 = idx1 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx1].R * 1.5f;
        float terrainHeight2 = idx2 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx2].R * 1.5f;
        float terrainHeight3 = idx3 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx3].R * 1.5f;
        float terrainHeight4 = idx4 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx4].R * 1.5f;

        // GD.Print("terrainHeight1: ",terrainHeight1);
        // GD.Print("terrainHeight2: ",terrainHeight2);
        // GD.Print("terrainHeight3: ",terrainHeight3);
        // GD.Print("terrainHeight4: ",terrainHeight4);

        float sx = xf * Client.Main.Constants.TERRAIN_SCALE;
        float sz = zf * Client.Main.Constants.TERRAIN_SCALE;
        float scaledSize = Client.Main.Constants.TERRAIN_SCALE * lodf;

        Vector3[] _tempTerrainVertices = new Vector3[4]
        {
            new Vector3(xf * Client.Main.Constants.TERRAIN_SCALE, terrainHeight1, zf * Client.Main.Constants.TERRAIN_SCALE),                     // bottom-left
			new Vector3((xf + 1) * Client.Main.Constants.TERRAIN_SCALE, terrainHeight2, zf * Client.Main.Constants.TERRAIN_SCALE),               // bottom-right
			new Vector3((xf + 1) * Client.Main.Constants.TERRAIN_SCALE, terrainHeight3, (zf + 1) * Client.Main.Constants.TERRAIN_SCALE),         // top-right
			new Vector3(xf * Client.Main.Constants.TERRAIN_SCALE, terrainHeight4, (zf + 1) * Client.Main.Constants.TERRAIN_SCALE)                // top-left
		};

        int[] _tempTerrainTriangles = new int[6]
        {
            0, 1, 2,  // First Triangle
			0, 2, 3   // Second Triangle
		};

        // Handle special height flag
        if (idx1 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx1].HasFlag(TWFlags.Height))
            _tempTerrainVertices[0].Y += SpecialHeight;
        if (idx2 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx2].HasFlag(TWFlags.Height))
            _tempTerrainVertices[1].Y += SpecialHeight;
        if (idx3 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx3].HasFlag(TWFlags.Height))
            _tempTerrainVertices[2].Y += SpecialHeight;
        if (idx4 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx4].HasFlag(TWFlags.Height))
            _tempTerrainVertices[3].Y += SpecialHeight;

        return (_tempTerrainVertices, _tempTerrainTriangles);
    }


    private void PrepareTileLights(int idx1, int idx2, int idx3, int idx4)
    {
        _tempTerrainLights[0] = idx1 < _backTerrainLight.Length ? _backTerrainLight[idx1] : Colors.White;
        _tempTerrainLights[1] = idx2 < _backTerrainLight.Length ? _backTerrainLight[idx2] : Colors.White;
        _tempTerrainLights[2] = idx3 < _backTerrainLight.Length ? _backTerrainLight[idx3] : Colors.White;
        _tempTerrainLights[3] = idx4 < _backTerrainLight.Length ? _backTerrainLight[idx4] : Colors.White;


        // GD.Print(_tempTerrainLights[0]);
        // GD.Print(_tempTerrainLights[1]);
        // GD.Print(_tempTerrainLights[2]);
        // GD.Print(_tempTerrainLights[3]);
    }

    private void ApplyAlphaToLights(byte alpha1, byte alpha2, byte alpha3, byte alpha4)
    {
        _tempTerrainLights[0].A = alpha1 / 255f;
        _tempTerrainLights[1].A = alpha2 / 255f;
        _tempTerrainLights[2].A = alpha3 / 255f;
        _tempTerrainLights[3].A = alpha4 / 255f;


        // GD.Print("_tempTerrainLights[0]: " + _tempTerrainLights[0]);
        // GD.Print("_tempTerrainLights[1]: " + _tempTerrainLights[1]);
        // GD.Print("_tempTerrainLights[2]: " + _tempTerrainLights[2]);
        // GD.Print("_tempTerrainLights[3]: " + _tempTerrainLights[3]);

        // GD.Print("_tempTerrainLights[0] alpha: " + _tempTerrainLights[0].A);
        // GD.Print("_tempTerrainLights[1] alpha: " + _tempTerrainLights[1].A);
        // GD.Print("_tempTerrainLights[2] alpha: " + _tempTerrainLights[2].A);
        // GD.Print("_tempTerrainLights[3] alpha: " + _tempTerrainLights[3].A);
    }

    private void RenderTerrainTile(float xf, float zf, int xi, int zi, float lodf, int lodi)
    {
        int idx1 = GetTerrainIndex(xi, zi);

        // Skip tile completely if both layers are empty
        int textureIndexMain = _mapping.Layer2[idx1];
        int textureIndexOverlay = _mapping.Layer1[idx1];

        if (textureIndexMain == 255 && textureIndexOverlay == 255)
            return; // Nothing to render

        if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.NoGround))
            return;

        int idx2 = GetTerrainIndex(xi + 1, zi);
        int idx3 = GetTerrainIndex(xi + 1, zi + 1);
        int idx4 = GetTerrainIndex(xi, zi + 1);

        // Prepare vertices and triangles
        var (vertices, triangles) = PrepareTileVertices(xi, zi, xf, zf, idx1, idx2, idx3, idx4, lodf);

        // Prepare lighting
        PrepareTileLights(idx1, idx2, idx3, idx4);

        // GD.Print("idx1: " + idx1);
        // GD.Print("idx2: " + idx2);
        // GD.Print("idx3: " + idx3);
        // GD.Print("idx4: " + idx4);

        // Alpha values
        byte alpha1 = idx1 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx1];
        byte alpha2 = idx2 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx2];
        byte alpha3 = idx3 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx3];
        byte alpha4 = idx4 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx4];

        // GD.Print("alpha1: 	" + alpha1);
        // GD.Print("alpha2: 	" + alpha2);
        // GD.Print("alpha3: 	" + alpha3);
        // GD.Print("alpha4: 	" + alpha4);

        bool isOpaque = alpha1 >= 255 && alpha2 >= 255 && alpha3 >= 255 && alpha4 >= 255;
        bool hasAlpha = alpha1 > 0 || alpha2 > 0 || alpha3 > 0 || alpha4 > 0;

        if (isOpaque)
        {
            RenderTexture(_mapping.Layer2[idx1], vertices, triangles, xf, zf, lodf, useAlpha: false);
        }
        else
        {
            RenderTexture(_mapping.Layer1[idx1], vertices, triangles, xf, zf, lodf, useAlpha: false);
        }

        if (hasAlpha && !isOpaque)
        {
            ApplyAlphaToLights(alpha1, alpha2, alpha3, alpha4);
            RenderTexture(_mapping.Layer2[idx1], vertices, triangles, xf, zf, lodf, useAlpha: true);
        }

    }

    private void RenderTexture(int textureIndex, Vector3[] vertices, int[] triangles, float xf, float zf, float lodScale = 1f, bool useAlpha = false)
    {

        if (textureIndex == 255)
            return;

        if (textureIndex == 255 || textureIndex < 0 || textureIndex >= _textures.Length || _textures[textureIndex] == null)
        {
            GD.Print($"Missing texture at index {textureIndex}");
            return;
        }

        Texture2D texture = _textures[textureIndex];

        if (texture == null)
        {
            GD.Print("null texture");
        }

        // Prepare UVs
        Vector2 texSize = texture.GetSize();
        float baseWidth = 64f / texSize.X;
        float baseHeight = 64f / texSize.Y;
        float suf = xf * baseWidth;
        float svf = zf * baseHeight;
        float uvWidth = baseWidth * lodScale;
        float uvHeight = baseHeight * lodScale;

        var _terrainTextureCoord = new Vector2[4];
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

        Color[] custom0 = new Color[4];

        for (int i = 0; i < 4; i++)
        {
            var c = _tempTerrainLights[i];

            // ΣΤΕΙΛΕ RAW LIGHT (0–255) ΣΤΟ SHADER
            custom0[i] = _tempTerrainLights[i];
        }

        // Create mesh
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices;
        arrays[(int)ArrayMesh.ArrayType.Color] = _tempTerrainLights;
        arrays[(int)ArrayMesh.ArrayType.Index] = triangles;
        arrays[(int)ArrayMesh.ArrayType.TexUV] = _terrainTextureCoord;
        arrays[(int)ArrayMesh.ArrayType.Normal] = normals;

        ArrayMesh mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Create material
        Material material;

        // Water special
        if (textureIndex == 5) // Water special
        {
            ShaderMaterial waterMat = new ShaderMaterial();
            waterMat.Shader = GD.Load<Shader>("res://Shaders/WaterShader.gdshader");
            waterMat.SetShaderParameter("_MainTex", texture);
            waterMat.SetShaderParameter("_DistortionFrequency", 5.0f);
            waterMat.SetShaderParameter("_DistortionAmplitude", 0.02f);
            waterMat.SetShaderParameter("_WaterSpeed", 0.1f);
            waterMat.SetShaderParameter("_ScrollSpeed", new Vector2(0.1f, 0.1f));
            waterMat.SetShaderParameter("_AlphaLayer", useAlpha);
            material = waterMat;
        }
        else if (useAlpha)
        {
            var mat = new StandardMaterial3D();

            mat.AlbedoTexture = texture;
            mat.VertexColorUseAsAlbedo = true;

            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Back;
            material = mat;
        }
        else
        {

            {
                var mat = new StandardMaterial3D();

                mat.AlbedoTexture = texture;
                mat.VertexColorUseAsAlbedo = true;

                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
                mat.CullMode = BaseMaterial3D.CullModeEnum.Back;
                material = mat;
            }
        }

        MeshInstance3D tile = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material
        };

        if (useAlpha) tile.Position = new Vector3(0, 0.01f, 0);
        else tile.Position = Vector3.Zero;

        tileContainer.AddChild(tile);
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
}