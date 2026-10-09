using Client.Data;
using Client.Data.BMD;
using Client.Data.OBJS;
using Client.Main;
using Client.Main.Utils;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public partial class ObjectControl : Node3D
{
    public static ObjectControl Instance { get; private set; }

    [Export] public short WorldIndex;
    public bool IsLoaded { get; private set; } = false;

    private Node3D _objectContainer;

    private Dictionary<int, (BMD bmd, ArrayMesh mesh)> _modelCache = new Dictionary<int, (BMD, ArrayMesh)>();

    public override async void _Ready()
    {
        Instance = this;

        // Το Godot Container. Δεν χρειάζεται πια να κάνουμε Z-Flip με το Scale(-1), 
        // γιατί τα μαθηματικά του Quat παρακάτω τα φτιάχνουν όλα τέλεια!
        _objectContainer = new Node3D();
        _objectContainer.Name = "ObjectsContainer";
        AddChild(_objectContainer);

        await LoadObjectsAsync();
        
        IsLoaded = true;
    }

    public async Task LoadObjectsAsync()
    {
        string worldFolder = $"World{WorldIndex}";
        string objectFolder = $"Object{WorldIndex}"; 
        string baseDataPath = Client.Main.Constants.DataPath;
        string fullPathWorldFolder = Path.Combine(baseDataPath, worldFolder);
        string fullPathObjectFolder = Path.Combine(baseDataPath, objectFolder);

        string objPath = Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.obj");

        if (!File.Exists(objPath))
        {
            GD.PrintErr($"[ObjectControl] OBJ file not found: {objPath}");
            return;
        }

        OBJReader objReader = new OBJReader();
        OBJ objData = await objReader.Load(objPath);

        if (objData == null || objData.Objects == null) return;

        BMDReader bmdReader = new BMDReader();
        int objectsSpawned = 0;

        foreach (IMapObject mapObj in objData.Objects)
        {
            int n = mapObj.Type + 1;

            string[] candidates = {
                $"Object{n}.bmd",
                $"Object{n:D2}.bmd",
                $"Object{n:D3}.bmd",
                $"Object{mapObj.Type:D2}.bmd"
            };

            string actualPath = null;
            foreach (var c in candidates)
            {
                actualPath = GetActualFilePath(fullPathObjectFolder, c);
                if (actualPath != null) break;
            }

            if (actualPath == null) continue;

            ArrayMesh objMesh;
            BMD bmdModel;

            // --- ΣΥΣΤΗΜΑ CACHE (ΔΙΟΡΘΩΜΕΝΟ) ---
            if (_modelCache.TryGetValue(mapObj.Type, out var cachedData))
            {
                bmdModel = cachedData.bmd;
                objMesh = cachedData.mesh;
            }
            else
            {
                bmdModel = await bmdReader.Load(actualPath);
                objMesh = await Client.Main.Utils.BMDMeshConverter.CreateMeshAsync(bmdModel, objectFolder);
                _modelCache[mapObj.Type] = (bmdModel, objMesh);
            }

            if (objMesh == null) continue;

            // --- ΕΛΕΓΧΟΣ & ΔΗΜΙΟΥΡΓΙΑ ANIMATION ---
            Node3D instanceRoot;
            bool hasAnimation = bmdModel.Actions != null && bmdModel.Actions.Length > 0 && bmdModel.Actions[0].NumAnimationKeys > 1;

            if (hasAnimation)
            {
                // Φτιάχνουμε Character Rig όπως κάναμε στα τέρατα!
                instanceRoot = Client.Main.Utils.BMDRigBuilder.CreateAnimatedCharacter(bmdModel, objMesh);
                
                AnimationPlayer animPlayer = instanceRoot.GetNode<AnimationPlayer>("AnimationPlayer");
                if (animPlayer.HasAnimation("Action_0"))
                {
                    animPlayer.Play("Action_0");
                    animPlayer.SpeedScale = 0.5f;
                    // ΤΡΙΚ: Βάζουμε ένα τυχαίο ξεκίνημα στο animation 
                    // για να μην κουνιούνται όλες οι σημαίες 100% ταυτόχρονα σαν ρομπότ!
                    animPlayer.Advance((float)GD.RandRange(0.0, 2.0));
                }
            }
            else
            {
                // Αν είναι απλό αντικείμενο, βάζουμε στατικό MeshInstance3D (γλιτώνει τεράστιο Performance)
                MeshInstance3D staticMesh = new MeshInstance3D();
                staticMesh.Mesh = objMesh;
                instanceRoot = staticMesh;
            }

            instanceRoot.Name = $"Obj_{mapObj.Type}_{objectsSpawned}";

            // --- ΤΟΠΟΘΕΤΗΣΗ ---
            float muX = mapObj.Position.X;
            float muY = mapObj.Position.Y; 
            float muHeight = mapObj.Position.Z; 
            
            float zFlickerFix = (objectsSpawned % 30) * 0.05f;

            instanceRoot.Position = new Vector3(muX, muHeight + zFlickerFix, -muY);

            // Περιστροφή (όπως τη φτιάξαμε πριν)
            Basis standUp = Basis.FromEuler(new Vector3(Mathf.DegToRad(-90), 0, 0));
            instanceRoot.Basis = new Basis(MuAngleToGodotQuat(mapObj.Angle)) * standUp;

            // Κλίμακα
            float s = mapObj.Scale * 100f; 
            if (s > 0.001f)
            {
                instanceRoot.Scale = new Vector3(s, s, s);
            }

            _objectContainer.AddChild(instanceRoot);
            objectsSpawned++;
        }

        GD.Print($"✔ Loaded {objectsSpawned} objects for World {WorldIndex}");
    }

    // Το μαθηματικό "Θαύμα" για να μπαίνουν όλα όρθια και να κοιτάνε σωστά
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

        // Godot.X = MU.X, Godot.Y = MU.Z, Godot.Z = -MU.Y
        return new Quaternion(qx, qz, -qy, qw);
    }

    private string GetActualFilePath(string fullDir, string fileName)
    {
        if (!Directory.Exists(fullDir)) return null;

        foreach (string file in Directory.GetFiles(fullDir))
        {
            if (string.Equals(Path.GetFileName(file), fileName, System.StringComparison.OrdinalIgnoreCase))
                return file; 
        }
        return null;
    }
}