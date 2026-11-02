using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// One layer of an effect graph, one VFX system: its bursts, how particles start and move, and how they draw.
    /// Offsets, speeds and sizes are in rig space (+Y up, metres, unscaled), angles in degrees and colours as the
    /// colour picker shows them. Life curves run over a particle's life from 0 to 1.
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
        public float dropChance;

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
        public List<ColorKey> colorOverLife = new();
        public AnimationCurve alphaOverLife = new();

        public Texture2D texture;
        public float softness;
        public bool upright;
        public float pivot;
        public Flipbook flipbook;

        public Texture2D negativeTexture;
        public Texture2D motionTexture;
        public float opacity;
        public float ambient;
        public Texture2D emissionRamp;
        [ColorUsage(true, true)] public Color emissionColor;
        public AnimationCurve heatOverLife = new();
        public AnimationCurve minHeatOverLife = new();

        [Tooltip("The chunks a debris layer draws, 1 to 4 meshes.")]
        public List<Mesh> meshes = new();
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

        /// <summary>The layer's shape; a ring always flies out along the ground.</summary>
        public EmitShape Shape => kind == EffectLayerKind.Ring ? EmitShape.Ring : shape;

        /// <summary>Which way the shape faces in rig space, +Y unless set.</summary>
        public Vector3 ShapeAxis => axis == Vector3.zero ? Vector3.up : axis.normalized;

        /// <summary>The shape's per-axis scale of start offsets, in shape space before it turns onto the axis.</summary>
        public Vector3 ShapeScale => shapeScale == Vector3.zero ? Vector3.one : shapeScale;

        public bool Glows => kind is EffectLayerKind.Glow or EffectLayerKind.Spark;

        /// <summary>Emits by rate while a stream plays, rather than on the Burst event.</summary>
        public bool Streams => !HasParent && (rate > 0f || ratePerMetre > 0f);

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

        public Gradient LifeGradient() => Gradient(colorOverLife, alphaOverLife);

        /// <summary>The flipbook frame over life, stopping short of the last frame's end so it never wraps.</summary>
        public AnimationCurve FrameCurve()
        {
            var last = flipbook.Frames - 0.01f;
            return AnimationCurve.Linear(0f, Mathf.Min(flipbook.from * flipbook.Frames, last), 1f, Mathf.Min(flipbook.to * flipbook.Frames, last));
        }

        /// <summary><paramref name="asset"/>, which the layer's kind needs; throws naming the field when it is empty.</summary>
        public T Required<T>(T asset, string field) where T : Object =>
            asset != null ? asset : throw new InvalidOperationException($"HyperRTS: {kind} layer '{name}' needs a {field}.");

        /// <summary>The layer's meshes, which a mesh output takes 1 to 4 of; throws naming the layer otherwise.</summary>
        public Mesh[] Meshes()
        {
            if (meshes.Count is 0 or > 4 || meshes.Contains(null))
            {
                throw new InvalidOperationException($"HyperRTS: {kind} layer '{name}' needs 1 to 4 meshes, all set.");
            }

            return meshes.ToArray();
        }

        private Gradient Gradient(List<ColorKey> colors, AnimationCurve alphas)
        {
            if (colors.Count > 8 || alphas.length > 8)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{name}' has a gradient of more than 8 keys.");
            }

            var gradient = new Gradient();
            var colorKeys = colors.Count == 0 ? new[] { new GradientColorKey(Color.white, 0f) } : colors.Select(k => new GradientColorKey(k.color.linear, k.time)).ToArray();
            var alphaKeys = alphas.length == 0 ? new[] { new GradientAlphaKey(1f, 0f) } : alphas.keys.Select(k => new GradientAlphaKey(k.value, k.time)).ToArray();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }
    }
}
