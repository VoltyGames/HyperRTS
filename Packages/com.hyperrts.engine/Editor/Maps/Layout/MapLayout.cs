using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>The whole map after <see cref="MapExpander"/> copied the authored sector: every feature in map space.</summary>
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

        /// <summary>The whole terrain: the playable area plus the scenery margin.</summary>
        public Rect TerrainBounds => new(-Size * 0.5f - Vector2.one * Terrain.margin, Size + Vector2.one * (Terrain.margin * 2f));

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

        /// <summary>Unity lays terrain grass out in patches this many detail texels square.</summary>
        public const int DetailPatch = 32;

        /// <summary>Splat texels per side: about three per metre keep road edges crisp.</summary>
        public int SplatResolution => Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(TerrainBounds.width * 2.5f)), 512, 2048);

        /// <summary>Grass and plant texels per side, a whole number of patches.</summary>
        public int DetailResolution => Mathf.CeilToInt(TerrainBounds.width * 1.25f / DetailPatch) * DetailPatch;

        /// <summary>Ordered by slot.</summary>
        public List<MapSpawn> Spawns { get; } = new();

        public List<MapCopy<MapResourceSpec>> Resources { get; set; } = new();
        public List<MapCopy<MapPropSpec>> Props { get; set; } = new();
        public List<MapCopy<MapRiverSpec>> Rivers { get; set; } = new();
        public List<MapCopy<MapRavineSpec>> Ravines { get; set; } = new();
        public List<MapCopy<MapHillSpec>> Hills { get; set; } = new();
        public List<MapCopy<MapRoadSpec>> Roads { get; set; } = new();
        public List<MapCopy<MapSettlementSpec>> Settlements { get; set; } = new();
        public List<MapCopy<MapWoodSpec>> Woods { get; set; } = new();
        public List<MapCopy<MapBeltSpec>> Belts { get; set; } = new();
        public List<MapCopy<MapFarmlandSpec>> Farmland { get; set; } = new();
        public List<MapCopy<MapAreaSpec>> Areas { get; set; } = new();
        public List<MapCopy<MapLineSpec>> Lines { get; set; } = new();
        public List<MapCopy<MapScatterSpec>> Scatters { get; set; } = new();
    }
}
