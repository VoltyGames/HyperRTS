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
        public float Datum => Terrain.height;

        public MapTerrainSpec Terrain => Spec.terrain;

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
