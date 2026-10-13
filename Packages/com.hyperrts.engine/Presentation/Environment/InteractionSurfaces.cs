using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>The two interaction render textures and the globals that publish them (layout in Trample.hlsl).</summary>
    public sealed class InteractionSurfaces : IDisposable
    {
        public static readonly int TrampleId = Shader.PropertyToID("_HyperRTSTrample");
        public static readonly int TracksId = Shader.PropertyToID("_HyperRTSTracks");
        public static readonly int RectId = Shader.PropertyToID("_HyperRTSInteractionRect");
        public static readonly int TracksTexelId = Shader.PropertyToID("_HyperRTSTracksTexel");

        /// <summary>No direction, standing plants.</summary>
        public static readonly Color TrampleClear = new(0.5f, 0.5f, 0f, 0f);

        public readonly RenderTexture Trample;
        public readonly RenderTexture Tracks;
        public readonly float4 Rect;
        public readonly InteractionQuality Quality;

        private readonly float2 _min;
        private readonly float2 _size;

        public InteractionSurfaces(float2 min, float2 size, InteractionQuality quality)
        {
            if (math.any(size <= 0f))
            {
                throw new ArgumentException($"HyperRTS: interaction maps need a map with a size, got {size}.");
            }

            _min = min;
            _size = size;
            Quality = quality;
            Rect = InteractionMath.Rect(min, size);
            var trampleSize = InteractionMath.TextureSize(size, InteractionMath.TrampleTexel(quality));
            var tracksSize = InteractionMath.TextureSize(size, InteractionMath.TracksTexel(quality));
            Trample = Create("HyperRTS Trample", trampleSize, GraphicsFormat.R8G8B8A8_UNorm);
            Tracks = Create("HyperRTS Tracks", tracksSize, GraphicsFormat.R8G8_UNorm);
        }

        public bool Matches(float2 min, float2 size, InteractionQuality quality) =>
            quality == Quality && min.Equals(_min) && size.Equals(_size);

        public void Publish()
        {
            Shader.SetGlobalTexture(TrampleId, Trample);
            Shader.SetGlobalTexture(TracksId, Tracks);
            Shader.SetGlobalVector(RectId, Rect);
            var texels = new float2(Tracks.width, Tracks.height);
            Shader.SetGlobalVector(TracksTexelId, new float4(texels, _size / texels));
        }

        /// <summary>No maps: shaders read a zero rect as nothing trampled.</summary>
        public static void Clear()
        {
            Shader.SetGlobalTexture(TrampleId, Texture2D.blackTexture);
            Shader.SetGlobalTexture(TracksId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, Vector4.zero);
            Shader.SetGlobalVector(TracksTexelId, Vector4.zero);
        }

        public void Dispose()
        {
            Release(Trample);
            Release(Tracks);
        }

        private static RenderTexture Create(string name, int2 size, GraphicsFormat format)
        {
            var texture = new RenderTexture(size.x, size.y, format, GraphicsFormat.None, 1)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        private static void Release(RenderTexture texture)
        {
            texture.Release();
            CoreUtils.Destroy(texture);
        }
    }
}
