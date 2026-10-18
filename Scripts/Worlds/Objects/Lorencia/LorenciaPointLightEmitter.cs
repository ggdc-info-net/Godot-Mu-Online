using Godot;
using System;

namespace MuGodot.Objects.Worlds.Lorencia;

public sealed partial class LorenciaPointLightEmitter : Node3D
{
    private OmniLight3D _light;
    private float _time;
    private float _timeOffset;
    private float _lightBaseEnergy;
    private float _flickerAmplitude;
    private bool _initialized;

    [Export] public short SourceType { get; set; }
    [Export] public int VariantIndex { get; set; }

    private readonly struct LightProfile
    {
        public readonly Color Color;
        public readonly float Range;
        public readonly float Energy;
        public readonly Vector3 LocalOffset;
        public readonly float FlickerAmplitude;

        public LightProfile(Color color, float range, float energy, Vector3 localOffset, float flickerAmplitude = 0f)
        {
            Color = color;
            Range = range;
            Energy = energy;
            LocalOffset = localOffset;
            FlickerAmplitude = flickerAmplitude;
        }
    }

    public static void AttachTo(Node3D instanceNode, short sourceType, int variantIndex = 0, string nodeName = null)
{
    if (instanceNode == null || !GodotObject.IsInstanceValid(instanceNode))
        return;

    string name = nodeName ?? (variantIndex == 0 ? "LorenciaPointLight" : $"LorenciaPointLight_{variantIndex}");

    // Αν υπάρχει ήδη, μην το ξαναπροσθέσεις
    if (instanceNode.GetNodeOrNull<LorenciaPointLightEmitter>(name) != null)
        return;

    var emitter = new LorenciaPointLightEmitter
    {
        Name = name,
        SourceType = sourceType,
        VariantIndex = variantIndex
    };

    instanceNode.AddChild(emitter);
}

    public override void _Ready()
    {
        Initialize();
    }

    public override void _Process(double delta)
    {
        if (!_initialized || _light == null)
            return;

        _time += (float)delta;
        float t = _time + _timeOffset;

        float lum = 0.9f
            + MathF.Sin(t * 1.8f) * _flickerAmplitude
            + MathF.Sin(t * 3.7f) * (_flickerAmplitude * 0.53f);

        _light.LightEnergy = _lightBaseEnergy * MathF.Max(0.6f, lum);

        
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        var profile = GetProfile(SourceType, VariantIndex);
        _lightBaseEnergy = profile.Energy;
        _flickerAmplitude = profile.FlickerAmplitude;

        unchecked
        {
            uint h = (uint)GetInstanceId().GetHashCode();
            h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15;
            _timeOffset = (h & 0x00FFFFFFu) / 16777215f * 200f;
        }

        _light = new OmniLight3D
        {
            Name = "PointLight",
            LightColor = profile.Color,
            OmniRange = profile.Range,
            LightEnergy = profile.Energy,
            LightSpecular = 0.0f,
            ShadowEnabled = false,
            Position = profile.LocalOffset
        };

        // --- DEBUG: Προσθήκη ορατής μπάλας για να βρεις το κέντρο ---
        // var debugSphere = new MeshInstance3D
        // {
        //     Name = "DebugCenterSphere",
        //     Mesh = new SphereMesh { Radius = 12.0f, Height = 0.4f }
        // };
        // // Δίνουμε ένα έντονο υλικό (π.χ. κόκκινο ή φωσφοριζέ) για να ξεχωρίζει
        // var mat = new StandardMaterial3D();
        // mat.AlbedoColor = Colors.Red;
        // mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        // debugSphere.MaterialOverride = mat;

        // _light.AddChild(debugSphere);
        // -----------------------------------------------------------

        // Ψάχνουμε το Skeleton3D στον γονέα
        Skeleton3D skeleton = null;
        Node parent = GetParent();
        while (parent != null)
        {
            skeleton = parent.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? parent as Skeleton3D;
            if (skeleton != null) break;
            parent = parent.GetParent();
        }

        if (skeleton != null)
        {
            // Βρίσκουμε το index του κόκαλου "Bone04"
            int boneIndex = skeleton.FindBone("Bone04");
            
            if (boneIndex != -1)
            {
                var boneAttachment = new BoneAttachment3D
                {
                    Name = "LightBoneAttachment",
                    BoneName = "Bone04" // Μπορούμε να βάλουμε απευθείας το όνομα!
                };

                skeleton.AddChild(boneAttachment);
                boneAttachment.AddChild(_light);
                _light.Position = profile.LocalOffset; // Μικρό fine-tuning αν χρειάζεται
            }
            else
            {
                // Fallback αν για κάποιο λόγο δεν το βρει
                AddChild(_light);
            }
        }
        else
        {
            AddChild(_light);
        }

        _initialized = true;

        if (_flickerAmplitude <= 0f)
        {
            SetProcess(false);
        }
    }

    private static LightProfile GetProfile(short type, int variantIndex)
    {
        return type switch
        {
            // Candle01 (Type 150) — Μικρό ζεστό φως που τρεμοπαίζει
            150 => new LightProfile(
                color: new Color(1f, 0.5f, 2.0f),
                range: 10.0f,
                energy: 15.0f,
                localOffset: new Vector3(0f, 3.0f, 0f),
                flickerAmplitude: 0f),

            // StreetLight01 (Type 90) — Σταθερό φως ψηλά στο φανάρι
            90 => new LightProfile(
                color: new Color(1.0f, 0.788f, 0.392f),
                range: 250.0f,
                energy: 300.0f,
                localOffset: new Vector3(0.0f, 0.0f, 0.0f),
                flickerAmplitude: 0f),

            // DungeonGate01 (Type 55) — Δύο φώτα στις κολώνες δεξιά και αριστερά
            55 => variantIndex == 0
                ? new LightProfile(
                    color: new Color(1f, 0.7f, 0.4f),
                    range: 4.5f,
                    energy: 2.5f,
                    localOffset: new Vector3(0.65f, 0.8f, 0f),
                    flickerAmplitude: 0.15f)
                : new LightProfile(
                    color: new Color(1f, 0.7f, 0.4f),
                    range: 4.5f,
                    energy: 2.5f,
                    localOffset: new Vector3(-0.65f, 0.8f, 0f),
                    flickerAmplitude: 0.15f),

            _ => new LightProfile(
                color: new Color(1f, 0.8f, 0.5f),
                range: 2.5f,
                energy: 1.0f,
                localOffset: Vector3.Zero)
        };
    }
}