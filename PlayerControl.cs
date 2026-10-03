using Godot;
using System.IO;
using System.Threading.Tasks;
using MuGodot; // Το namespace από τα έτοιμα scripts που βρήκες!

public partial class PlayerControl : Node3D
{
    private MuModelBuilder _modelBuilder;
    private Client.Data.BMD.BMD _skeletonBmd;
    private MuAnimatedMeshController _mainAnimController; 
    private Node3D _weaponPivot;
    private MeshInstance3D _weaponMesh;

    public override async void _Ready()
    {
        _modelBuilder = new MuModelBuilder();
        await SpawnAsync();
    }

    public async Task SpawnAsync()
    {
        _skeletonBmd = await _modelBuilder.LoadBmdAsync("Player/Player.bmd");
        if (_skeletonBmd == null) return;

        // 2. Τα κομμάτια της πανοπλίας του Dark Wizard
        string[] parts = { 
            "HelmClass01.bmd", 
            "ArmorClass01.bmd", 
            "PantClass01.bmd", 
            "GloveClass01.bmd", 
            "BootClass01.bmd" 
        };
        
        foreach (var part in parts)
        {
            var partBmd = await _modelBuilder.LoadBmdAsync($"Player/{part}");
            if (partBmd == null) continue;

            var materials = await _modelBuilder.LoadModelTexturesAsync($"Player/{part}");

            MeshInstance3D meshInst = new MeshInstance3D { Name = part.Replace(".bmd", "") };
            AddChild(meshInst);

            MuAnimatedMeshController animController = new MuAnimatedMeshController();
            animController.Name = part.Replace(".bmd", "") + "_Anim";
            AddChild(animController);

            animController.Initialize(
                _modelBuilder,
                partBmd,
                materials,
                actionIndex: 1, 
                animationSourceBmd: _skeletonBmd,
                useRealtimeInterpolation: true 
            );

            animController.RegisterInstance(meshInst);

            if (_mainAnimController == null) 
                _mainAnimController = animController;
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

    public override void _Process(double delta)
    {
        if (_weaponPivot != null && _skeletonBmd != null && _mainAnimController != null)
        {
            int currentAction = _mainAnimController.ActionIndex;
            float currentFrame = _mainAnimController.FramePosition;

            int targetBoneIndex = 33; 

            if (_modelBuilder.TryGetBoneTransform(_skeletonBmd, currentAction, currentFrame, targetBoneIndex, out Transform3D boneTransform))
            {
                _weaponPivot.Transform = boneTransform;
            }
        }
    }
}