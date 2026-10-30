using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// One layer of an effect graph, one VFX system: its bursts, how particles start and move, and how they draw.
    /// Offsets, speeds and sizes are in rig space (+Y up, metres, unscaled), angles in degrees and colours as the
    /// colour picker shows them. Assets are named by path or GUID.
    /// </summary>
    [Serializable]
    public sealed class EffectLayer
    {
        public string name;
        public string kind;
        public int capacity;
        public List<EffectBurst> bursts = new();
        public float rate;
        public float ratePerMetre;
        public string parent;
        public float dropChance;

        public FloatRange lifetime;
        public FloatRange size;
        public float aspect;
        public ColorRange color;
        public FloatRange rotation;
        public FloatRange spin;

        public Vector3 origin;
        public string shape;
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

        public List<CurveKey> sizeOverLife = new();
        public List<ColorKey> colorOverLife = new();
        public List<CurveKey> alphaOverLife = new();

        public string texture;
        public float softness;
        public bool upright;
        public float pivot;
        public Flipbook flipbook;

        public string negativeTexture;
        public string motionTexture;
        public float opacity;
        public float ambient;
        public string emissionRamp;
        public Color emissionColor;
        public List<CurveKey> heatOverLife = new();
        public List<CurveKey> minHeatOverLife = new();

        public string mesh;
        public List<string> meshNames = new();
        public float metallic;
        public float smoothness;
        public bool ground;
        public float groundHeight;
        public float bounce;
        public float friction;

        public float length;
        public float lengthPerSpeed;

        public float distortion;
        public Vector2 scroll;

        public EffectLayerKind Kind => Parse<EffectLayerKind>(kind, "kind");

        /// <summary>The layer's shape; a ring always flies out along the ground, so it names none.</summary>
        public EmitShape Shape
        {
            get
            {
                if (Kind != EffectLayerKind.Ring)
                {
                    return Parse<EmitShape>(shape, "shape");
                }

                return string.IsNullOrEmpty(shape) ? EmitShape.Ring : throw new InvalidOperationException($"HyperRTS: ring layer '{name}' takes no shape.");
            }
        }

        /// <summary>Which way the shape faces in rig space, +Y unless set.</summary>
        public Vector3 ShapeAxis => axis == Vector3.zero ? Vector3.up : axis.normalized;

        /// <summary>The shape's per-axis scale of start offsets, in shape space before it turns onto the axis.</summary>
        public Vector3 ShapeScale => shapeScale == Vector3.zero ? Vector3.one : shapeScale;

        public bool Glows => Kind is EffectLayerKind.Glow or EffectLayerKind.Spark;

        /// <summary>Emits by rate while a stream plays, rather than on the Burst event.</summary>
        public bool Streams => !HasParent && (rate > 0f || ratePerMetre > 0f);

        /// <summary>Emitted by another layer's particles over their life, rather than by the rig.</summary>
        public bool HasParent => !string.IsNullOrEmpty(parent);

        /// <summary>Kinds drawn through one of the engine's Shader Graphs rather than a built-in VFX output.</summary>
        public bool DrawsShaderGraph => Kind is EffectLayerKind.Distort or EffectLayerKind.SixWay or EffectLayerKind.Ring;

        /// <summary>Kinds whose alpha must be stored per particle, read by children or a Shader Graph, not only drawn.</summary>
        public bool KeepsAlpha => Kind == EffectLayerKind.Source || DrawsShaderGraph;

        public AnimationCurve SizeCurve() => Curve(sizeOverLife);

        public AnimationCurve HeatCurve() => Curve(heatOverLife);

        public AnimationCurve MinHeatCurve() => Curve(minHeatOverLife);

        public AnimationCurve AlphaCurve() => Curve(alphaOverLife);

        /// <summary>The frames a random-frame particle picks from: from and to as shares of the sheet.</summary>
        public FloatRange RandomFrames()
        {
            var first = flipbook.from * flipbook.Frames;
            return new FloatRange(first, Mathf.Max(first, flipbook.to * flipbook.Frames - 0.01f));
        }

        public Gradient LifeGradient() => Gradient(colorOverLife, alphaOverLife);

        /// <summary>The flipbook frame over life, stopping short of the last frame's end so it never wraps.</summary>
        public AnimationCurve FrameCurve()
        {
            var last = flipbook.Frames - 0.01f;
            return AnimationCurve.Linear(0f, Mathf.Min(flipbook.from * flipbook.Frames, last), 1f, Mathf.Min(flipbook.to * flipbook.Frames, last));
        }

        public T Load<T>(string reference, string field) where T : Object
        {
            var path = Regex.IsMatch(reference ?? "", "^[0-9a-f]{32}$") ? AssetDatabase.GUIDToAssetPath(reference) : reference;
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{name}' {field} '{reference}' is no {typeof(T).Name}.");
            }

            return asset;
        }

        /// <summary>The meshes named in meshNames from the mesh file, or its only mesh; a mesh output takes up to 4.</summary>
        public Mesh[] Meshes()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(Load<Object>(mesh, "mesh"))).OfType<Mesh>().ToArray();
            if (meshNames.Count == 0)
            {
                return all.Length == 1 ? all : throw new InvalidOperationException($"HyperRTS: layer '{name}' mesh '{mesh}' holds {all.Length} meshes; list meshNames.");
            }

            if (meshNames.Count > 4)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{name}' lists {meshNames.Count} meshNames, more than 4.");
            }

            return meshNames.Select(wanted => all.FirstOrDefault(m => m.name == wanted)
                ?? throw new InvalidOperationException($"HyperRTS: layer '{name}' mesh '{mesh}' has no {wanted}.")).ToArray();
        }

        private static AnimationCurve Curve(List<CurveKey> keys) => new(keys.Select(k => new Keyframe(k.time, k.value)).ToArray());

        private Gradient Gradient(List<ColorKey> colors, List<CurveKey> alphas)
        {
            if (colors.Count > 8 || alphas.Count > 8)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{name}' has a gradient of more than 8 keys.");
            }

            var gradient = new Gradient();
            var colorKeys = colors.Count == 0 ? new[] { new GradientColorKey(Color.white, 0f) } : colors.Select(k => new GradientColorKey(k.color.linear, k.time)).ToArray();
            var alphaKeys = alphas.Count == 0 ? new[] { new GradientAlphaKey(1f, 0f) } : alphas.Select(k => new GradientAlphaKey(k.value, k.time)).ToArray();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        private T Parse<T>(string text, string field) where T : struct, Enum
        {
            if (Enum.TryParse(text, true, out T value) && !int.TryParse(text, out _))
            {
                return value;
            }

            throw new InvalidOperationException($"HyperRTS: layer '{name}' has {field} '{text}', not one of {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }
    }
}
