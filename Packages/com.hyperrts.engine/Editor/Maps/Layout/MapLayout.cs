using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout;

/// <summary>The whole map after <see cref="MapSymmetryCopier"/> copied the authored sector: every feature in map space.</summary>
public sealed class MapLayout
{
    public MapLayout(MapSpec spec, MapSymmetry symmetry)
    {
        Spec = spec;
        Symmetry = symmetry;
    }

    public MapSpec Spec { get; }

    public MapSymmetry Symmetry { get; }

    public Vector2 Size => Spec.size.ToVector2();

    /// <summary>The playable area.</summary>
    public Rect Bounds => new(-Size * 0.5f, Size);

    /// <summary>The whole terrain: the playable area plus the backdrop.</summary>
    public Rect TerrainBounds => new(-Size * 0.5f - Vector2.one * Terrain.backdropWidth, Size + Vector2.one * (Terrain.backdropWidth * 2f));

    /// <summary>
    /// Terrain height of world y = 0. The terrain is lowered by it so open ground sits at y = 0, where the
    /// camera and the rest of the engine expect the ground.
    /// </summary>
    public float GroundLevel => Terrain.height;

    /// <summary>World position of the terrain's corner, lowered so open ground sits at y = 0.</summary>
    public Vector3 TerrainOrigin => new(TerrainBounds.xMin, -GroundLevel, TerrainBounds.yMin);

    /// <summary>World height of the water surface.</summary>
    public float WaterHeight => Terrain.waterLevel - GroundLevel;

    public MapTerrainSpec Terrain => Spec.terrain;

    /// <summary>Ground texture texels per side: about three per metre keep road edges crisp.</summary>
    public int GroundTextureResolution => Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(TerrainBounds.width * 2.5f)), 512, 2048);

    /// <summary>Grass coverage texels per side: the kernel's dithered, filtered lookup hides the 0.8 m texels.</summary>
    public int GrassResolution => Mathf.CeilToInt(TerrainBounds.width * 1.25f);

    /// <summary>Ordered by slot.</summary>
    public List<MapSpawn> Spawns { get; } = [];

    public List<MapCopy<MapResourceSpec>> Resources { get; set; } = [];
    public List<MapCopy<MapPropSpec>> Props { get; set; } = [];
    public List<MapCopy<MapRiverSpec>> Rivers { get; set; } = [];
    public List<MapCopy<MapPondSpec>> Ponds { get; set; } = [];
    public List<MapCopy<MapRavineSpec>> Ravines { get; set; } = [];
    public List<MapCopy<MapHillSpec>> Hills { get; set; } = [];
    public List<MapCopy<MapRoadSpec>> Roads { get; set; } = [];
    public List<MapCopy<MapSettlementSpec>> Settlements { get; set; } = [];
    public List<MapCopy<MapWoodSpec>> Woods { get; set; } = [];
    public List<MapCopy<MapBeltSpec>> Belts { get; set; } = [];
    public List<MapCopy<MapFarmlandSpec>> Farmland { get; set; } = [];
    public List<MapCopy<MapAreaSpec>> Areas { get; set; } = [];
    public List<MapCopy<MapRowSpec>> Rows { get; set; } = [];
    public List<MapCopy<MapScatterSpec>> Scatters { get; set; } = [];
}
