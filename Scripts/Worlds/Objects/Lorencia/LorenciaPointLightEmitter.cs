using Godot;
using System;
using System.Collections.Generic;

namespace MuGodot.Objects.Worlds.Lorencia;

public sealed partial class LorenciaPointLightEmitter : Node3D
{
    private readonly List<OmniLight3D> _lights = new();
    private float _time;
    private float _timeOffset;
    private float _lightBaseEnergy;
    private float _flickerAmplitude;
    private bool _initialized;

    [Export] public short SourceType { get; set; }
    [Export] public int VariantIndex { get; set; }

    // Δομή για κάθε ξεχωριστό κερί/φως και το κόκαλο του
    public readonly struct CandleBoneInfo
    {
        public readonly string BoneName;
        public readonly Vector3 LocalOffset;

        public CandleBoneInfo(string boneName, Vector3 localOffset)
        {
            BoneName = boneName;
            LocalOffset = localOffset;
        }
    }

    private readonly struct LightProfile
    {
        public readonly Color Color;
        public readonly float Range;
        public readonly float Energy;
        public readonly CandleBoneInfo[] Candles; // Λίστα με τα κεριά και τα κόκαλά τους
        public readonly float FlickerAmplitude;

        public LightProfile(Color color, float range, float energy, CandleBoneInfo[] candles, float flickerAmplitude = 0f)
        {
            Color = color;
            Range = range;
            Energy = energy;
            Candles = candles;
            FlickerAmplitude = flickerAmplitude;
        }
    }

    public static void AttachTo(Node3D instanceNode, short sourceType, int variantIndex = 0, string nodeName = null)
    {
        if (instanceNode == null || !GodotObject.IsInstanceValid(instanceNode))
            return;

        string name = nodeName ?? (variantIndex == 0 ? "LorenciaPointLight" : $"LorenciaPointLight_{variantIndex}");

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
        if (!_initialized || _lights.Count == 0)
            return;

        _time += (float)delta;
        float t = _time + _timeOffset;

        float lum = 0.9f
            + MathF.Sin(t * 1.8f) * _flickerAmplitude
            + MathF.Sin(t * 3.7f) * (_flickerAmplitude * 0.53f);

        float currentEnergy = _lightBaseEnergy * MathF.Max(0.6f, lum);

        foreach (var light in _lights)
        {
            if (GodotObject.IsInstanceValid(light))
            {
                light.LightEnergy = currentEnergy;
            }
        }
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

        // Αναζήτηστη Skeleton3D στον γονέα
        Skeleton3D skeleton = null;
        Node parent = GetParent();
        while (parent != null)
        {
            skeleton = parent.GetNodeOrNull<Skeleton3D>("Skeleton3D") ?? parent as Skeleton3D;
            if (skeleton != null) break;
            parent = parent.GetParent();
        }

        // Δημιουργία φώτων ανάλογα με τα κεριά και τα κόκαλα τους
        foreach (var candle in profile.Candles)
        {
            var light = new OmniLight3D
            {
                Name = $"PointLight_{candle.BoneName}",
                LightColor = profile.Color,
                OmniRange = profile.Range,
                LightEnergy = profile.Energy,
                LightSpecular = 0.0f,
                ShadowEnabled = false,
                Position = candle.LocalOffset
            };

            _lights.Add(light);

            if (skeleton != null && !string.IsNullOrEmpty(candle.BoneName))
            {
                int boneIndex = skeleton.FindBone(candle.BoneName);
                if (boneIndex != -1)
                {
                    var boneAttachment = new BoneAttachment3D
                    {
                        Name = $"Attachment_{candle.BoneName}",
                        BoneName = candle.BoneName
                    };
                    skeleton.AddChild(boneAttachment);
                    boneAttachment.AddChild(light);
                    light.Position = candle.LocalOffset; // Fine-tuning offset πάνω στο κόκαλο
                }
                else
                {
                    // Αν δεν βρει το συγκεκριμένο κόκαλο, το βάζει στο Root του αντικειμένου
                    AddChild(light);
                }
            }
            else
            {
                AddChild(light);
            }
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
            // Παράδειγμα για το Κηροπήγιο με τα 3 κεριά και τα δικά τους κόκαλα
            150 => new LightProfile(
                color: new Color(1.0f, 0.788f, 0.392f),
                range: 250.0f,
                energy: 300.0f,
                candles: new CandleBoneInfo[]
                {
                    new CandleBoneInfo("Bone03", new Vector3(0.0f, 0.1f, 0.0f)), // Κερί 1 στο Bone01
                    new CandleBoneInfo("Bone05", new Vector3(0.0f, 0.1f, 0.0f)), // Κερί 2 στο Bone02
                    new CandleBoneInfo("Bone07", new Vector3(0.0f, 0.1f, 0.0f))  // Κερί 3 στο Bone03
                },
                flickerAmplitude: 0.2f),

            // StreetLight01 (Type 90) - Χωρίς σκελετό ή με απλό κόκαλο/root
            90 => new LightProfile(
                color: new Color(1.0f, 0.788f, 0.392f),
                range: 250.0f,
                energy: 300.0f,
                candles: new CandleBoneInfo[]
                {
                    new CandleBoneInfo("Bone04", Vector3.Zero)
                },
                flickerAmplitude: 0f),

            _ => new LightProfile(
                color: new Color(1f, 0.8f, 0.5f),
                range: 2.5f,
                energy: 1.0f,
                candles: new CandleBoneInfo[]
                {
                    new CandleBoneInfo("", Vector3.Zero)
                })
        };
    }
}