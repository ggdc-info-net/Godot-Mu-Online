using Godot;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using MuGodot; 
using Client.Data.ATT; // Απαραίτητο για το TWFlags

public partial class PlayerControl : Node3D
{
    private MuModelBuilder _modelBuilder;
    private Client.Data.BMD.BMD _skeletonBmd;
    private MuAnimatedMeshController _mainAnimController; 
    private Node3D _weaponPivot;
    private MeshInstance3D _weaponMesh;
    private CameraControl _playerCamera;
    
    // Λίστα με όλους τους AnimControllers για να τους αλλάζουμε Action (1 = Idle, 15 = Walk)
    private List<MuAnimatedMeshController> _idleControllers = new List<MuAnimatedMeshController>();
    private List<MuAnimatedMeshController> _walkControllers = new List<MuAnimatedMeshController>();
    // --- ΜΕΤΑΒΛΗΤΕΣ ΚΙΝΗΣΗΣ ---
    [Export] public float MoveSpeed = 400f; // Ταχύτητα κίνησης
    private Queue<Vector2I> _path = new Queue<Vector2I>();
    private Vector3 _moveTarget;
    private bool _isMoving = false;
    private float _targetYaw = 0f;

    public override async void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _modelBuilder = new MuModelBuilder();
        await SpawnAsync();
        
        // Setup Κάμερας
        _playerCamera = new CameraControl { Name = "MainCamera" };
        GetParent().AddChild(_playerCamera); 
        _playerCamera.Initialize(() => this.GlobalPosition);

        // Περιμένουμε το Terrain
        while (TerrainControl.Instance == null || !TerrainControl.Instance.IsLoaded)
        {
            await ToSignal(GetTree(), "process_frame");
        }
        
        TeleportToTile(138, 138, 45f);
        _playerCamera.ResetAndUpdate();
    }

    public async Task SpawnAsync()
    {
        _skeletonBmd = await _modelBuilder.LoadBmdAsync("Player/Player.bmd");
        if (_skeletonBmd == null) return;

        string[] parts = { 
            "HelmClass01.bmd", 
            "ArmorClass01.bmd", 
            "PantClass01.bmd", 
            "GloveClass01.bmd", 
            "BootClass01.bmd" 
        };

        float syncStartTime = Time.GetTicksMsec() * 0.001f;

        foreach (var part in parts)
        {
            var partBmd = await _modelBuilder.LoadBmdAsync($"Player/{part}");
            if (partBmd == null) continue;

            var materials = await _modelBuilder.LoadModelTexturesAsync($"Player/{part}");

            MeshInstance3D meshInst = new MeshInstance3D { Name = part.Replace(".bmd", "") };
            AddChild(meshInst);

            // --- 1. Φτιάχνουμε τον ελεγκτή για το IDLE (Στάση αναμονής = 1) ---
            // --- 1. Φτιάχνουμε τον ελεγκτή για το IDLE ---
            MuAnimatedMeshController idleAnim = new MuAnimatedMeshController();
            idleAnim.Name = part.Replace(".bmd", "") + "_Idle";
            AddChild(idleAnim);
            
            idleAnim.Initialize(
                _modelBuilder,
                partBmd,
                materials,
                actionIndex: 1, 
                animationSpeed: 7f,     // <--- ΠΡΟΣΘΗΚΗ: Ταχύτητα Animation
                subFrameSamples: 8,        // <--- ΠΡΟΣΘΗΚΗ: Απαλότητα (Blending)
                animationSourceBmd: _skeletonBmd,
                syncStartTimeSeconds: syncStartTime,
                useRealtimeInterpolation: true 
            );
            
            idleAnim.RegisterInstance(meshInst);
            _idleControllers.Add(idleAnim);

            // --- 2. Φτιάχνουμε τον ελεγκτή για το WALK ---
            MuAnimatedMeshController walkAnim = new MuAnimatedMeshController();
            walkAnim.Name = part.Replace(".bmd", "") + "_Walk";
            AddChild(walkAnim);
            
            walkAnim.Initialize(
                _modelBuilder,
                partBmd,
                materials,
                actionIndex: 50, 
                animationSpeed: 7f,     // <--- ΠΡΟΣΘΗΚΗ: Ταχύτητα Animation
                subFrameSamples: 8,        // <--- ΠΡΟΣΘΗΚΗ: Απαλότητα (Blending)
                animationSourceBmd: _skeletonBmd,
                syncStartTimeSeconds: syncStartTime,
                useRealtimeInterpolation: true 
            );
            
            walkAnim.RegisterInstance(meshInst);
            walkAnim.SetExternalAnimationEnabled(false); 
            _walkControllers.Add(walkAnim);

            // Κρατάμε τον Idle ως αρχικό ελεγκτή
            if (_mainAnimController == null) 
                _mainAnimController = idleAnim;
        }

        await EquipWeaponAsync("Item/Staff01.bmd", 33); 
    }

    private async Task EquipWeaponAsync(string relativePath, int boneIndex)
    {
        var weaponBmd = await _modelBuilder.LoadBmdAsync(relativePath);
        if (weaponBmd == null) return;
        var materials = await _modelBuilder.LoadModelTexturesAsync(relativePath);

        _weaponPivot = new Node3D { Name = "WeaponPivot" };
        AddChild(_weaponPivot);

        _weaponMesh = new MeshInstance3D { Name = "WeaponMesh" };
        _weaponPivot.AddChild(_weaponMesh);

        var mesh = _modelBuilder.BuildMesh(weaponBmd, 0, 0f);
        _weaponMesh.Mesh = mesh;

        for (int i = 0; i < materials.Length; i++)
        {
            _weaponMesh.SetSurfaceOverrideMaterial(i, materials[i]);
        }
        _weaponMesh.RotationDegrees = new Vector3(0, 0, 45);
    }

    // --- ΚΛΙΚ ΠΟΝΤΙΚΙΟΥ ---
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            // Αριστερό ή Δεξί κλικ για κίνηση (Άλλαξέ το αν θέλεις μόνο το αριστερό: MouseButton.Left)
            if (mouseButton.ButtonIndex == MouseButton.Left || mouseButton.ButtonIndex == MouseButton.Right)
            {
                TrySetMoveTarget(mouseButton.Position);
            }
        }
    }

    public override void _Process(double delta)
    {
        // 1. Έλεγχος Όπλου
        if (_weaponPivot != null && _skeletonBmd != null && _mainAnimController != null)
        {
            int currentAction = _mainAnimController.ActionIndex;
            float currentFrame = _mainAnimController.FramePosition;
            if (_modelBuilder.TryGetBoneTransform(_skeletonBmd, currentAction, currentFrame, 33, out Transform3D boneTransform))
            {
                _weaponPivot.Transform = boneTransform;
            }
        }

        // 2. Ενημέρωση Κίνησης Παίκτη
        UpdateMovement(delta);

        // 3. Ενημέρωση Κάμερας
        if (_playerCamera != null)
        {
            _playerCamera.Update(delta);
        }
    }

    // ==========================================
    // ΣΥΣΤΗΜΑ ΚΙΝΗΣΗΣ & PATHFINDING
    // ==========================================

    private void UpdateMovement(double delta)
    {
        if (!_isMoving) return;

        float step = MoveSpeed * (float)delta;
        Vector3 current = this.GlobalPosition;
        
        // Υπολογισμός απόστασης (2D στον άξονα X, Z)
        Vector2 current2D = new Vector2(current.X, current.Z);
        Vector2 target2D = new Vector2(_moveTarget.X, _moveTarget.Z);
        float distance = current2D.DistanceTo(target2D);

        if (distance <= step)
        {
            // Φτάσαμε στο τρέχον Tile
            current.X = _moveTarget.X;
            current.Z = _moveTarget.Z;

            if (_path.Count > 0)
            {
                // Πάμε στο επόμενο Tile
                SetMoveTargetFromTile(_path.Dequeue());
            }
            else
            {
                // Φτάσαμε στον τελικό προορισμό
                _isMoving = false;
                SetAnimationState(false);
            }
        }
        else
        {
            // Προχωράμε προς το Tile
            Vector2 dir = (target2D - current2D).Normalized();
            current.X += dir.X * step;
            current.Z += dir.Y * step;
            
            // Υπολογισμός κατεύθυνσης (γωνία)
            _targetYaw = Mathf.Atan2(dir.X, dir.Y);
        }

        // Προσαρμογή στο ύψος της πίστας
        if (TerrainControl.Instance != null && TerrainControl.Instance.IsLoaded)
        {
            current.Y = TerrainControl.Instance.GetHeight(current.X, -current.Z);
        }

        this.GlobalPosition = current;

        // Ομαλή Περιστροφή Παίκτη
        float currentYaw = this.Rotation.Y;
        float newYaw = Mathf.LerpAngle(currentYaw, _targetYaw, 15f * (float)delta);
        this.Rotation = new Vector3(0, newYaw, 0);
    }

    private void SetMoveTargetFromTile(Vector2I tile)
    {
        float scale = Client.Main.Constants.TERRAIN_SCALE;
        float targetX = (tile.X + 0.5f) * scale;
        float targetZ = (tile.Y + 0.5f) * scale;
        
        float height = 0f;
        if (TerrainControl.Instance != null && TerrainControl.Instance.IsLoaded)
        {
            height = TerrainControl.Instance.GetHeight(targetX, targetZ);
        }

        _moveTarget = new Vector3(targetX, height, -targetZ);
    }

    private void TrySetMoveTarget(Vector2 mousePosition)
    {
        if (!TryRaycastTerrain(mousePosition, out Vector3 hitPos))
            return;

        float scale = Client.Main.Constants.TERRAIN_SCALE;
        int tileX = Mathf.Clamp(Mathf.FloorToInt(hitPos.X / scale), 0, Client.Main.Constants.TERRAIN_SIZE - 1);
        int tileY = Mathf.Clamp(Mathf.FloorToInt(-hitPos.Z / scale), 0, Client.Main.Constants.TERRAIN_SIZE - 1);

        if (!IsTileWalkable(tileX, tileY)) return;

        int startX = Mathf.Clamp(Mathf.FloorToInt(this.GlobalPosition.X / scale), 0, Client.Main.Constants.TERRAIN_SIZE - 1);
        int startY = Mathf.Clamp(Mathf.FloorToInt(-this.GlobalPosition.Z / scale), 0, Client.Main.Constants.TERRAIN_SIZE - 1);

        var startTile = new Vector2I(startX, startY);
        var targetTile = new Vector2I(tileX, tileY);

        var path = FindPath(startTile, targetTile);
        if (path.Count == 0 && startTile != targetTile) return;

        _path.Clear();
        foreach (var p in path) _path.Enqueue(p);

        if (_path.Count > 0 && !_isMoving)
        {
            SetMoveTargetFromTile(_path.Dequeue());
            _isMoving = true;
            SetAnimationState(true);
        }
    }

    private bool TryRaycastTerrain(Vector2 mousePosition, out Vector3 hitPosition)
{
    hitPosition = Vector3.Zero;
    if (_playerCamera == null || _playerCamera.Camera == null) return false;
    
    Camera3D cam = _playerCamera.Camera;
    Vector3 rayOrigin = cam.ProjectRayOrigin(mousePosition);
    Vector3 rayDir = cam.ProjectRayNormal(mousePosition).Normalized();

    float traveled = 0f;
    Vector3 lastPos = rayOrigin;
    
    // Ασφαλής υπολογισμός αρχικού ύψους
    float lastTerrainHeight = 0f;
    if (TerrainControl.Instance != null && TerrainControl.Instance.IsLoaded)
    {
        lastTerrainHeight = TerrainControl.Instance.GetHeight(lastPos.X, -lastPos.Z);
    }
    float lastDiff = lastPos.Y - lastTerrainHeight;

    // Προσομοίωση Raycast: Προχωράμε βήμα-βήμα (raymarching)
    while (traveled < 10000f) 
    {
        traveled += 10f; 
        Vector3 pos = rayOrigin + (rayDir * traveled);
        
        // Ασφαλής υπολογισμός ύψους για το νέο βήμα
        float terrainHeight = 0f;
        if (TerrainControl.Instance != null && TerrainControl.Instance.IsLoaded)
        {
            terrainHeight = TerrainControl.Instance.GetHeight(pos.X, -pos.Z);
        }
        float diff = pos.Y - terrainHeight;

        if (lastDiff > 0f && diff <= 0f) // Η ακτίνα πέρασε κάτω από το έδαφος!
        {
            hitPosition = pos;
            return true;
        }
        lastPos = pos;
        lastDiff = diff;
    }
    return false;
}

    private List<Vector2I> FindPath(Vector2I start, Vector2I target)
    {
        if (start == target) return new List<Vector2I> { target };

        var frontier = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        frontier.Enqueue(start, 0f);

        Vector2I[] neighbors = {
            new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1),
            new Vector2I(1, 1), new Vector2I(1, -1), new Vector2I(-1, 1), new Vector2I(-1, -1)
        };

        int visited = 0;
        while (frontier.Count > 0 && visited < 4000)
        {
            visited++;
            Vector2I current = frontier.Dequeue();
            if (current == target) break;

            float currentG = gScore[current];
            foreach (var nOffset in neighbors)
            {
                Vector2I n = current + nOffset;
                if (n.X < 0 || n.Y < 0 || n.X >= Client.Main.Constants.TERRAIN_SIZE || n.Y >= Client.Main.Constants.TERRAIN_SIZE)
                    continue;

                if (!IsTileWalkable(n.X, n.Y)) continue;

                float moveCost = (nOffset.X != 0 && nOffset.Y != 0) ? 1.414f : 1f;
                float tentativeG = currentG + moveCost;

                if (gScore.TryGetValue(n, out float knownG) && tentativeG >= knownG)
                    continue;

                gScore[n] = tentativeG;
                cameFrom[n] = current;
                float f = tentativeG + Mathf.Max(Mathf.Abs(n.X - target.X), Mathf.Abs(n.Y - target.Y));
                frontier.Enqueue(n, f);
            }
        }

        if (!cameFrom.ContainsKey(target)) return new List<Vector2I>();

        var path = new List<Vector2I>();
        Vector2I node = target;
        while (node != start)
        {
            path.Add(node);
            node = cameFrom[node];
        }
        path.Reverse();
        return path;
    }

    private bool IsTileWalkable(int x, int y)
    {
        if (TerrainControl.Instance == null || !TerrainControl.Instance.IsLoaded) return false;
        var flags = TerrainControl.Instance.RequestTerraingFlag(x, y);
        return (flags & TWFlags.NoMove) == 0;
    }

    private void SetAnimationState(bool moving)
{
    // Ανάλογα με το αν κινούμαστε, ανοίγουμε τους μεν και κλείνουμε τους δε
    foreach (var controller in _idleControllers)
    {
        controller.SetExternalAnimationEnabled(!moving);
    }
    
    foreach (var controller in _walkControllers)
    {
        controller.SetExternalAnimationEnabled(moving);
    }

    // ΠΟΛΥ ΣΗΜΑΝΤΙΚΟ: Αλλάζουμε τον "κύριο" ελεγκτή (ώστε το όπλο να συγχρονίζεται με το Walk ή το Idle)
    if (_idleControllers.Count > 0 && _walkControllers.Count > 0)
    {
        _mainAnimController = moving ? _walkControllers[0] : _idleControllers[0];
    }
}

    // ==========================================
    
    public void TeleportToTile(int tileX, int tileY, float facingDegrees = 0f)
    {
        float scale = Client.Main.Constants.TERRAIN_SCALE;
        float gridX = (tileX + 0.5f) * scale;
        float gridZ = (tileY + 0.5f) * scale;

        float height = 0f;
        if (TerrainControl.Instance != null && TerrainControl.Instance.IsLoaded)
        {
            height = TerrainControl.Instance.GetHeight(gridX, gridZ); 
        }

        Vector3 targetPos = new Vector3(gridX, height, -gridZ);
        this.GlobalPosition = targetPos;

        _targetYaw = Mathf.DegToRad(facingDegrees);
        this.Rotation = new Vector3(0, _targetYaw, 0);

        _path.Clear();
        _isMoving = false;
        SetAnimationState(false);
    }
}