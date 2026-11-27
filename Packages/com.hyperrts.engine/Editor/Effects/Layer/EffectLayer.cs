using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Layer;

/// <summary>
/// One layer of an effect graph, one VFX system. Offsets, speeds and sizes are in rig space (+Y up, metres),
/// angles in degrees; life curves run from 0 to 1.
/// </summary>
[Serializable]
public sealed class EffectLayer
{
    public string name;
    public EffectLayerKind kind;
    public int capacity;
    public List<EffectBurst> bursts = new();
    public float rate;
    public float ratePerMetre;
    public string parent;

    public FloatRange lifetime;
    public FloatRange size;
    public float aspect;
    public ColorRange color;
    public FloatRange rotation;
    public FloatRange spin;

    public Vector3 origin;

    [Tooltip("Where particles start. Rings ignore it: they always fly out along the ground.")]
    public EmitShape shape;

    public float radius;
    public bool rim;
    public float spread;
    public Vector3 axis;
    public Vector3 shapeScale;
    public FloatRange speed;
    public float gravity;
    public float drag;
    public bool dragBySpeed;
    public float wind;
    public Vector3 drift;
    public EffectNoise noise;
    public FloatRange orbit;

    public AnimationCurve sizeOverLife = new();
    public AnimationCurve alphaOverLife = new();

    public Texture2D texture;
    public float softness;

    [Tooltip("Which way the quads face. A flat puff is projected onto the ground, so it follows the terrain and takes its light.")]
    public EffectLayerOrient orient;

    public float pivot;
    public Flipbook flipbook;

    public Texture2D motionTexture;

    [ShowForKinds(EffectLayerKind.SixWay, EffectLayerKind.Ring)]
    public SixWaySettings sixWay = new();

    [ShowForKinds(EffectLayerKind.Debris)]
    public DebrisSettings debris = new();

    public bool ground;
    public float groundHeight;
    public float bounce;
    public float friction;

    [ShowForKinds(EffectLayerKind.Spark)]
    public StretchSettings stretch = new();

    [ShowForKinds(EffectLayerKind.Distort)]
    public DistortSettings distort = new();

    /// <summary>The layer's shape; a ring always flies out along the ground.</summary>
    public EmitShape Shape => kind == EffectLayerKind.Ring ? EmitShape.Ring : shape;

    /// <summary>Which way the shape faces in rig space, +Y unless set.</summary>
    public Vector3 ShapeAxis => axis == Vector3.zero ? Vector3.up : axis.normalized;

    /// <summary>The shape's per-axis scale of start offsets, in shape space before it turns onto the axis.</summary>
    public Vector3 ShapeScale => shapeScale == Vector3.zero ? Vector3.one : shapeScale;

    public bool Glows => kind is EffectLayerKind.Glow or EffectLayerKind.Spark;

    /// <summary>The VFX Orient mode the layer's quads turn by, or null for meshes and sources, which draw no quad.</summary>
    public string OrientMode => kind switch
    {
        EffectLayerKind.Debris or EffectLayerKind.Source => null,
        EffectLayerKind.Spark => "AlongVelocity",
        _ => orient switch
        {
            EffectLayerOrient.Upright => "FixedAxis",
            EffectLayerOrient.Flat => "Advanced",
            _ => "FaceCameraPlane",
        },
    };

    /// <summary>A flat puff, drawn as a decal projected down onto the ground rather than as a quad.</summary>
    public bool Projected => kind == EffectLayerKind.Puff && orient == EffectLayerOrient.Flat;

    /// <summary>Emits by rate while a stream plays, rather than on the Burst event.</summary>
    public bool Streams => !HasParent && HasRate;

    /// <summary>Emits by a rate a second, the rig's rate per metre included.</summary>
    public bool HasRate => rate > 0f || ratePerMetre > 0f;

    /// <summary>Emitted by another layer's particles over their life, rather than by the rig.</summary>
    public bool HasParent => !string.IsNullOrEmpty(parent);

    /// <summary>Kinds drawn through one of the engine's Shader Graphs rather than a built-in VFX output.</summary>
    public bool DrawsShaderGraph => kind is EffectLayerKind.Distort or EffectLayerKind.SixWay or EffectLayerKind.Ring;

    /// <summary>Kinds whose alpha must be stored per particle, read by children or a Shader Graph, not only drawn.</summary>
    public bool KeepsAlpha => kind == EffectLayerKind.Source || DrawsShaderGraph;

    /// <summary>The frames a random-frame particle picks from: from and to as shares of the sheet.</summary>
    public FloatRange RandomFrames()
    {
        var first = flipbook.from * flipbook.Frames;
        return new FloatRange(first, Mathf.Max(first, flipbook.to * flipbook.Frames - 0.01f));
    }

    /// <summary>The alpha over life as a gradient of white, for a built-in output to multiply into the colour.</summary>
    public Gradient AlphaGradient()
    {
        var gradient = new Gradient();
        var alphaKeys = alphaOverLife.keys.Select(k => new GradientAlphaKey(k.value, k.time)).ToArray();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f) }, alphaKeys);
        return gradient;
    }

    /// <summary>
    /// The flipbook frame over life, ending exactly on the last frame: any fraction past it would blend toward the
    /// next frame, which wraps to the first.
    /// </summary>
    public AnimationCurve FrameCurve()
    {
        var last = flipbook.Frames - 1f;
        return AnimationCurve.Linear(0f, Mathf.Min(flipbook.from * flipbook.Frames, last), 1f, Mathf.Min(flipbook.to * flipbook.Frames, last));
    }

    /// <summary>What a build of this layer would refuse, each naming the layer and field; empty when it is sound.</summary>
    public List<string> Problems()
    {
        var problems = new List<string>();
        if (capacity <= 0)
        {
            problems.Add($"layer '{name}' needs a capacity.");
        }

        AddSpawnProblems(problems);
        AddAssetProblems(problems);
        if (flipbook.random && flipbook.randomStart)
        {
            problems.Add($"layer '{name}' flipbook is both random and randomStart; pick one.");
        }

        if (alphaOverLife.length > 8 && !KeepsAlpha)
        {
            problems.Add($"layer '{name}' has a gradient of more than 8 keys.");
        }

        return problems;
    }

    private void AddSpawnProblems(List<string> problems)
    {
        if (HasParent)
        {
            if (rate <= 0f || ratePerMetre != 0f || bursts.Count > 0)
            {
                problems.Add($"child layer '{name}' needs a rate a second per parent particle, and no bursts or rate per metre.");
            }
        }
        else if (HasRate && bursts.Count > 0)
        {
            problems.Add($"layer '{name}' has both bursts and a rate; split it in two.");
        }
        else if (!HasRate && bursts.Count == 0)
        {
            problems.Add($"layer '{name}' needs bursts, a rate or a parent.");
        }
    }

    private void AddAssetProblems(List<string> problems)
    {
        switch (kind)
        {
            case EffectLayerKind.Glow or EffectLayerKind.Spark or EffectLayerKind.Puff or EffectLayerKind.Distort:
                Need(problems, texture, nameof(texture));
                break;
            case EffectLayerKind.SixWay or EffectLayerKind.Ring:
                if (sixWay.opacity <= 0f || sixWay.ambient <= 0f)
                {
                    problems.Add($"{kind} layer '{name}' needs an opacity and an ambient, as its six-way material's Density and Ambient Strength.");
                }

                Need(problems, texture, nameof(texture));
                Need(problems, sixWay.negativeTexture, nameof(sixWay.negativeTexture));
                Need(problems, motionTexture, nameof(motionTexture));
                break;
            case EffectLayerKind.Debris when debris.meshes.Count is 0 or > 4 || debris.meshes.Contains(null):
                problems.Add($"{kind} layer '{name}' needs 1 to 4 meshes, all set.");
                break;
        }
    }

    private void Need(List<string> problems, UnityEngine.Object asset, string field)
    {
        if (asset == null)
        {
            problems.Add($"{kind} layer '{name}' needs a {field}.");
        }
    }
}
