using Client.Data;
using Client.Data.BMD;
using Client.Data.OBJS;
using Client.Main;
using Godot;
using MuGodot;
using MuGodot.Objects.Worlds; // Βεβαιώσου ότι έχεις το using για τους κανόνες σου
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class ObjectControl : Node3D
{
    public static ObjectControl Instance { get; private set; }

    [Export] public short WorldIndex;
    public bool IsLoaded { get; private set; } = false;

    private Node3D _objectContainer;
    private Camera3D _camera;
    private ObjectCullingSystem _cullingSystem;

    private Dictionary<int, (BMD bmd, ArrayMesh mesh)> _modelCache = new Dictionary<int, (BMD, ArrayMesh)>();
    
    private struct AnimObj {
        public AnimationPlayer Anim;
        public Vector3 Pos;
    }
    private List<AnimObj> _animatedObjects = new List<AnimObj>();

    public override async void _Ready()
    {
        Instance = this;
        _objectContainer = new Node3D();
        _objectContainer.Name = "ObjectsContainer";
        AddChild(_objectContainer);

        _cullingSystem = GetNodeOrNull<ObjectCullingSystem>("../ObjectCullingSystem");
        if (_cullingSystem == null)
        {
            _cullingSystem = new ObjectCullingSystem();
            _cullingSystem.Name = "ObjectCullingSystem";
            GetParent().CallDeferred("add_child", _cullingSystem);
        }

        _camera = GetViewport().GetCamera3D();
        await LoadObjectsAsync();
        
        if (_camera != null && _cullingSystem != null)
        {
            _cullingSystem.Initialize(_camera);
            _cullingSystem.RebuildCaches(_objectContainer);
        }

        IsLoaded = true;
    }

    public override void _Process(double delta)
    {
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera3D();
            if (_camera != null && _cullingSystem != null)
            {
                _cullingSystem.Initialize(_camera);
                _cullingSystem.RebuildCaches(_objectContainer);
            }
        }
        
        if (_camera != null)
        {
            Vector3 camPos = _camera.GlobalPosition;

            // 1. Ενημέρωση του Culling System για τα στατικά
            if (_cullingSystem != null)
            {
                _cullingSystem.Update(delta, camPos);
            }

            // 2. Έλεγχος απόστασης για τα Animated (2000m)
            float animDistSq = 2000f * 2000f; 

            foreach (var obj in _animatedObjects)
            {
                if (obj.Anim == null || !GodotObject.IsInstanceValid(obj.Anim)) continue;

                bool isNear = obj.Pos.DistanceSquaredTo(camPos) <= animDistSq;
                
                if (isNear)
                {
                    if (obj.Anim.ProcessMode != Node.ProcessModeEnum.Inherit)
                    {
                        obj.Anim.ProcessMode = Node.ProcessModeEnum.Inherit;
                    }

                    if (!obj.Anim.IsPlaying())
                    {
                        obj.Anim.Play("Action_0"); 
                    }
                }
                else
                {
                    if (obj.Anim.ProcessMode != Node.ProcessModeEnum.Disabled)
                    {
                        obj.Anim.ProcessMode = Node.ProcessModeEnum.Disabled;
                    }
                }
            }
        }
    }

    public async Task LoadObjectsAsync()
    {
        string worldFolder = $"World{WorldIndex}";
        string objectFolder = $"Object{WorldIndex}"; 
        string baseDataPath = Client.Main.Constants.DataPath;
        string fullPathWorldFolder = Path.Combine(baseDataPath, worldFolder);
        string fullPathObjectFolder = Path.Combine(baseDataPath, objectFolder);

        string objPath = Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.obj");
        if (!File.Exists(objPath)) return;

        OBJReader objReader = new OBJReader();
        OBJ objData = await objReader.Load(objPath);
        if (objData == null || objData.Objects == null) return;

        var availableFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(fullPathObjectFolder))
        {
            foreach (string file in Directory.GetFiles(fullPathObjectFolder))
                availableFiles[Path.GetFileName(file)] = file;
        }

        var uniqueTypes = objData.Objects.Select(o => o.Type).Distinct().ToList();
        BMDReader bmdReader = new BMDReader();

        var loadTasks = uniqueTypes.Select(async type =>
        {
            int n = type + 1;
            string[] candidates = { $"Object{n}.bmd", $"Object{n:D2}.bmd", $"Object{n:D3}.bmd", $"Object{type:D2}.bmd" };
            
            string actualPath = candidates.FirstOrDefault(c => availableFiles.ContainsKey(c));
            if (actualPath != null) actualPath = availableFiles[actualPath];

            if (actualPath != null && !_modelCache.ContainsKey(type))
            {
                try
                {
                    BMD bmdModel = await bmdReader.Load(actualPath);
                    ArrayMesh objMesh = await Client.Main.Utils.BMDMeshConverter.CreateMeshAsync(bmdModel, objectFolder);
                    
                    if (objMesh != null)
                    {
                        lock (_modelCache) { _modelCache[type] = (bmdModel, objMesh); }
                    }
                }
                catch (Exception) { }
            }
        });

        await Task.WhenAll(loadTasks);

        // ==========================================
        // 1️⃣ ΒΗΜΑ 1: Παίρνουμε τους κανόνες της πίστας
        // ==========================================
        var worldRules = WorldObjectRulesResolver.Resolve(WorldIndex);

        var staticInstancesByType = new Dictionary<int, List<IMapObject>>();
        var animatedObjectsList = new List<IMapObject>();
        var specialObjectsList = new List<IMapObject>();

        // ==========================================
        // 2️⃣ ΒΗΜΑ 2: Διαχωρισμός μέσω του IsSpecialObject
        // ==========================================
        foreach (IMapObject mapObj in objData.Objects)
        {
            if (!_modelCache.TryGetValue(mapObj.Type, out var cachedData)) continue;

            bool hasAnimation = cachedData.bmd.Actions != null && cachedData.bmd.Actions.Length > 0 && cachedData.bmd.Actions[0].NumAnimationKeys > 1;

            if (worldRules.IsSpecialObject(mapObj.Type))
            {
                specialObjectsList.Add(mapObj);
                GD.Print($"✔ Special Object detected! Type: {mapObj.Type}");
                GD.Print($"Object Type: {mapObj.Type}");
            }
            else if (hasAnimation)
            {
                animatedObjectsList.Add(mapObj);
            }
            else
            {
                if (!staticInstancesByType.ContainsKey(mapObj.Type))
                    staticInstancesByType[mapObj.Type] = new List<IMapObject>();
                
                staticInstancesByType[mapObj.Type].Add(mapObj);
            }
        }

        Basis standUp = Basis.FromEuler(new Vector3(Mathf.DegToRad(-90), 0, 0));
        int totalSpawned = 0;

        // --- ΣΤΑΤΙΚΑ ΑΝΤΙΚΕΙΜΕΝΑ (CHUNKS ΓΙΑ FPS) ---
        float chunkSize = 16f; 
        var chunkGroups = staticInstancesByType
            .SelectMany(kvp => kvp.Value.Select(mapObj => new { Type = kvp.Key, Obj = mapObj }))
            .GroupBy(x => new {
                ChunkX = Mathf.FloorToInt(x.Obj.Position.X / chunkSize),
                ChunkZ = Mathf.FloorToInt(x.Obj.Position.Y / chunkSize),
                x.Type
            });

        foreach (var group in chunkGroups)
        {
            int type = group.Key.Type;
            int chunkX = group.Key.ChunkX;
            int chunkZ = group.Key.ChunkZ;
            var instances = group.Select(x => x.Obj).ToList();
            var cachedData = _modelCache[type];

            MultiMesh multimesh = new MultiMesh();
            multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            multimesh.Mesh = cachedData.mesh;
            multimesh.InstanceCount = instances.Count;

            for (int i = 0; i < instances.Count; i++)
            {
                var mapObj = instances[i];
                float muX = mapObj.Position.X;
                float muY = mapObj.Position.Y; 
                float muHeight = mapObj.Position.Z; 

                Vector3 pos = new Vector3(muX, muHeight, -muY);
                Basis rot = new Basis(MuAngleToGodotQuat(mapObj.Angle)) * standUp;
                float s = mapObj.Scale * 100f;
                if (s < 0.001f) s = 1.0f;

                Transform3D t = new Transform3D(rot, pos);
                t = t.ScaledLocal(new Vector3(s, s, s));

                multimesh.SetInstanceTransform(i, t);
            }

            MultiMeshInstance3D multiMeshInst = new MultiMeshInstance3D();
            multiMeshInst.Multimesh = multimesh;
            multiMeshInst.Name = $"Obj_Chunk_{chunkX}_{chunkZ}_{type}";
            multiMeshInst.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; 
            
            _objectContainer.AddChild(multiMeshInst);
            totalSpawned += instances.Count;
        }

        // --- ANIMATED ΑΝΤΙΚΕΙΜΕΝΑ ---
        int animatedCount = 0;
        foreach (var mapObj in animatedObjectsList)
        {
            var cachedData = _modelCache[mapObj.Type];
            Node3D instanceRoot = Client.Main.Utils.BMDRigBuilder.CreateAnimatedCharacter(cachedData.bmd, cachedData.mesh);
            
            AnimationPlayer animPlayer = instanceRoot.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (animPlayer != null && animPlayer.HasAnimation("Action_0"))
            {
                var anim = animPlayer.GetAnimation("Action_0");
                if (anim != null) 
                {
                    anim.LoopMode = Animation.LoopModeEnum.Linear;
                    animPlayer.Play("Action_0");
                    animPlayer.SpeedScale = 0.50f; 
                    float randomStart = (float)GD.RandRange(0.0, anim.Length);
                    animPlayer.Seek(randomStart, true);
                }
            }

            instanceRoot.Name = $"AnimObj_{mapObj.Type}_{animatedCount}";

            float muX = mapObj.Position.X;
            float muY = mapObj.Position.Y; 
            float muHeight = mapObj.Position.Z; 
            
            Vector3 worldPos = new Vector3(muX, muHeight, -muY);
            instanceRoot.Position = worldPos;
            instanceRoot.Basis = new Basis(MuAngleToGodotQuat(mapObj.Angle)) * standUp;

            float s = mapObj.Scale * 100f; 
            if (s > 0.001f)
            {
                instanceRoot.Scale = new Vector3(s, s, s);
            }

            _objectContainer.AddChild(instanceRoot);
            
            if (animPlayer != null)
            {
                _animatedObjects.Add(new AnimObj { Anim = animPlayer, Pos = worldPos });
            }

            animatedCount++;
            totalSpawned++;
        }

        // ==========================================
        // 3️⃣ ΒΗΜΑ 3: Δημιουργία Special Objects & Εφαρμογή Rules/Effects
        // ==========================================
        int specialCount = 0;
        foreach (var mapObj in specialObjectsList)
        {
            var cachedData = _modelCache[mapObj.Type];

            MeshInstance3D specialInst = new MeshInstance3D();
            specialInst.Mesh = cachedData.mesh;
            specialInst.Name = $"SpecialObj_{mapObj.Type}_{specialCount}";

            float muX = mapObj.Position.X;
            float muY = mapObj.Position.Y;
            float muHeight = mapObj.Position.Z;

            Vector3 worldPos = new Vector3(muX, muHeight, -muY);
            specialInst.Position = worldPos;
            specialInst.Basis = new Basis(MuAngleToGodotQuat(mapObj.Angle)) * standUp;

            float s = mapObj.Scale * 100f;
            if (s > 0.001f) specialInst.Scale = new Vector3(s, s, s);

            _objectContainer.AddChild(specialInst);

            // Εφαρμογή των εφέ, φώτων ή ρυθμίσεων από το rule script της πίστας
            worldRules.ApplyInstanceRules(mapObj.Type, specialInst);

            specialCount++;
            totalSpawned++;
        }

        GD.Print($"✔ Loaded {totalSpawned} total objects for World {WorldIndex} (Special: {specialCount}, Animated: {animatedCount})");
    }

    private Quaternion MuAngleToGodotQuat(System.Numerics.Vector3 angleDeg)
    {
        const float deg2Rad = MathF.PI / 180f;
        float halfPitch = angleDeg.X * deg2Rad * 0.5f;
        float halfYaw = angleDeg.Y * deg2Rad * 0.5f;
        float halfRoll = angleDeg.Z * deg2Rad * 0.5f;

        float sr = MathF.Sin(halfPitch);
        float cr = MathF.Cos(halfPitch);
        float sp = MathF.Sin(halfYaw);
        float cp = MathF.Cos(halfYaw);
        float sy = MathF.Sin(halfRoll);
        float cy = MathF.Cos(halfRoll);

        float qw = cr * cp * cy + sr * sp * sy;
        float qx = sr * cp * cy - cr * sp * sy;
        float qy = cr * sp * cy + sr * cp * sy;
        float qz = cr * cp * sy - sr * sp * cy;

        return new Quaternion(qx, qz, -qy, qw);
    }
}