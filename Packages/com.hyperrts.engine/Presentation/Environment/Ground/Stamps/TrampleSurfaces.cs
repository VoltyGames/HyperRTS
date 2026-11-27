using System;
using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Quality;
using HyperRTS.Presentation.Rendering;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HyperRTS.Presentation.Environment.Ground.Stamps;

/// <summary>The four trample render textures and the globals that publish them.</summary>
public sealed class TrampleSurfaces : IDisposable
{
    /// <summary>Largest side of either map; bigger maps get coarser texels.</summary>
    public const int MaxTextureSize = 8192;

    private const string RectName = "_HyperRTSTrampleRect";

    // The four maps share one rect.
    public static readonly MapTextureGlobal TrampleGlobal = new("_HyperRTSTrample", RectName);
    public static readonly MapTextureGlobal TracksGlobal = new("_HyperRTSTracks", RectName);
    public static readonly MapTextureGlobal WakeGlobal = new("_HyperRTSWake", RectName);
    public static readonly MapTextureGlobal StainsGlobal = new("_HyperRTSStains", RectName);
    public static readonly int TracksTexelId = Shader.PropertyToID("_HyperRTSTracksTexel");

    /// <summary>No direction, standing plants.</summary>
    public static readonly Color TrampleClear = new(0.5f, 0.5f, 0f, 0f);

    /// <summary>No ruts or scorch, and level ground in alpha.</summary>
    public static readonly Color TracksClear = new(0f, 0f, 0f, TrampleStamps.GroundLevelBias);

    public readonly RenderTexture Trample;
    public readonly RenderTexture Tracks;
    public readonly RenderTexture Wake;

    /// <summary>Rubble, ash, fuel and wet mud (RGBA), kept for the whole match.</summary>
    public readonly RenderTexture Stains;
    public readonly float4 Rect;
    public readonly TrampleQuality Quality;

    private readonly float2 _min;
    private readonly float2 _size;

    public TrampleSurfaces(float2 min, float2 size, TrampleQuality quality)
    {
        if (math.any(size <= 0f))
        {
            throw new ArgumentException($"HyperRTS: trample maps need a map with a size, got {size}.");
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
        var stainsSize = TextureSize(size, texels.Stains);
        Stains = Create("HyperRTS Stains", stainsSize, GraphicsFormat.R8G8B8A8_UNorm);
    }

    /// <summary>Metres per texel of each map at <paramref name="quality"/>; None has no maps.</summary>
    public static (float Trample, float Tracks, float Stains) Texels(TrampleQuality quality) => quality switch
    {
        TrampleQuality.Full => (0.5f, 0.2f, 0.4f),
        TrampleQuality.Low => (1f, 0.4f, 0.8f),
        _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "None has no trample maps."),
    };

    /// <summary>Texels needed to cover <paramref name="mapSize"/> metres, capped at <see cref="MaxTextureSize"/>.</summary>
    public static int2 TextureSize(float2 mapSize, float texel) =>
        math.clamp((int2)math.ceil(mapSize / texel), 1, MaxTextureSize);

    public bool Matches(float2 min, float2 size, TrampleQuality quality) =>
        quality == Quality && min.Equals(_min) && size.Equals(_size);

    public void Publish()
    {
        TrampleGlobal.Publish(Trample, Rect);
        TracksGlobal.Publish(Tracks, Rect);
        WakeGlobal.Publish(Wake, Rect);
        StainsGlobal.Publish(Stains, Rect);
        var texels = new float2(Tracks.width, Tracks.height);
        Shader.SetGlobalVector(TracksTexelId, new float4(texels, _size / texels));
    }

    public static void Clear()
    {
        TrampleGlobal.Clear();
        TracksGlobal.Clear();
        WakeGlobal.Clear();
        StainsGlobal.Clear();
        Shader.SetGlobalVector(TracksTexelId, Vector4.zero);
    }

    public void Dispose()
    {
        RenderTargets.Free(Trample);
        RenderTargets.Free(Tracks);
        RenderTargets.Free(Wake);
        RenderTargets.Free(Stains);
    }

    private static RenderTexture Create(string name, int2 size, GraphicsFormat format) =>
        RenderTargets.Create(name, size.x, size.y, format, FilterMode.Bilinear, TextureWrapMode.Clamp);
}
