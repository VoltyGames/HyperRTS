using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Quality;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Interaction
{
    /// <summary>The four interaction render textures and the globals that publish them.</summary>
    public sealed class InteractionSurfaces : IDisposable
    {
        /// <summary>Largest side of either map; bigger maps get coarser texels.</summary>
        public const int MaxTextureSize = 8192;

        public static readonly int TrampleId = Shader.PropertyToID("_HyperRTSTrample");
        public static readonly int TracksId = Shader.PropertyToID("_HyperRTSTracks");
        public static readonly int WakeId = Shader.PropertyToID("_HyperRTSWake");
        public static readonly int RemainsId = Shader.PropertyToID("_HyperRTSRemains");
        public static readonly int RectId = Shader.PropertyToID("_HyperRTSInteractionRect");
        public static readonly int TracksTexelId = Shader.PropertyToID("_HyperRTSTracksTexel");

        /// <summary>No direction, standing plants.</summary>
        public static readonly Color TrampleClear = new(0.5f, 0.5f, 0f, 0f);

        public readonly RenderTexture Trample;
        public readonly RenderTexture Tracks;
        public readonly RenderTexture Wake;

        /// <summary>Rubble, ash, fuel and wet mud (RGBA), kept for the whole match.</summary>
        public readonly RenderTexture Remains;
        public readonly float4 Rect;
        public readonly InteractionQuality Quality;

        private readonly float2 _min;
        private readonly float2 _size;

        /// <summary>Copies the remains map to a kernel: compute shaders don't see globals. Black while there is none.</summary>
        public static void BindRemains(ComputeShader compute, int kernel)
        {
            var remains = Shader.GetGlobalTexture(RemainsId);
            compute.SetTexture(kernel, RemainsId, remains != null ? remains : Texture2D.blackTexture);
        }

        /// <summary>Whether a trample map is published, which changes every frame.</summary>
        public static bool Published => Shader.GetGlobalVector(RectId).z > 0f;

        /// <summary>Copies the trample map and its rect to a kernel. Black and a zero rect while none is published.</summary>
        public static void BindTrample(ComputeShader compute, int kernel)
        {
            var trample = Shader.GetGlobalTexture(TrampleId);
            var on = trample != null && Published;
            compute.SetTexture(kernel, TrampleId, on ? trample : Texture2D.blackTexture);
            compute.SetVector(RectId, on ? Shader.GetGlobalVector(RectId) : Vector4.zero);
        }

        public InteractionSurfaces(float2 min, float2 size, InteractionQuality quality)
        {
            if (math.any(size <= 0f))
            {
                throw new ArgumentException($"HyperRTS: interaction maps need a map with a size, got {size}.");
            }

            _min = min;
            _size = size;
            Quality = quality;
            Rect = TerrainHeightInput.Rect(min, size);
            var texels = Texels(quality);
            var trampleSize = TextureSize(size, texels.Trample);
            var tracksSize = TextureSize(size, texels.Tracks);
            Trample = Create("HyperRTS Trample", trampleSize, GraphicsFormat.R8G8B8A8_UNorm);
            Tracks = Create("HyperRTS Tracks", tracksSize, GraphicsFormat.R8G8B8A8_UNorm);
            // Foam is soft, so the wake map shares the trample map's texel.
            Wake = Create("HyperRTS Wake", trampleSize, GraphicsFormat.R8_UNorm);
            var remainsSize = TextureSize(size, texels.Remains);
            Remains = Create("HyperRTS Remains", remainsSize, GraphicsFormat.R8G8B8A8_UNorm);
        }

        /// <summary>Metres per texel of each map at <paramref name="quality"/>; Off has no maps.</summary>
        public static (float Trample, float Tracks, float Remains) Texels(InteractionQuality quality) => quality switch
        {
            InteractionQuality.Full => (0.5f, 0.2f, 0.4f),
            InteractionQuality.Low => (1f, 0.4f, 0.8f),
            _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Off has no interaction maps."),
        };

        /// <summary>Texels needed to cover <paramref name="mapSize"/> metres, capped at <see cref="MaxTextureSize"/>.</summary>
        public static int2 TextureSize(float2 mapSize, float texel) =>
            math.clamp((int2)math.ceil(mapSize / texel), 1, MaxTextureSize);

        public bool Matches(float2 min, float2 size, InteractionQuality quality) =>
            quality == Quality && min.Equals(_min) && size.Equals(_size);

        public void Publish()
        {
            Shader.SetGlobalTexture(TrampleId, Trample);
            Shader.SetGlobalTexture(TracksId, Tracks);
            Shader.SetGlobalTexture(WakeId, Wake);
            Shader.SetGlobalTexture(RemainsId, Remains);
            Shader.SetGlobalVector(RectId, Rect);
            var texels = new float2(Tracks.width, Tracks.height);
            Shader.SetGlobalVector(TracksTexelId, new float4(texels, _size / texels));
        }

        /// <summary>No maps: shaders read a zero rect as nothing trampled.</summary>
        public static void Clear()
        {
            Shader.SetGlobalTexture(TrampleId, Texture2D.blackTexture);
            Shader.SetGlobalTexture(TracksId, Texture2D.blackTexture);
            Shader.SetGlobalTexture(WakeId, Texture2D.blackTexture);
            Shader.SetGlobalTexture(RemainsId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, Vector4.zero);
            Shader.SetGlobalVector(TracksTexelId, Vector4.zero);
        }

        public void Dispose()
        {
            Release(Trample);
            Release(Tracks);
            Release(Wake);
            Release(Remains);
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
