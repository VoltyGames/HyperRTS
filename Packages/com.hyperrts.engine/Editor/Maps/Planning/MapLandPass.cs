using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// One step of land planning: it claims the land it is for, taking only what earlier passes left, and may place
    /// props and plant trees. Choices are made in the authored sector and copied by the symmetry. Constructors only
    /// keep the context; the work happens in <see cref="Plan"/>, in <see cref="MapLandPlanner"/>'s order.
    /// </summary>
    public abstract class MapLandPass
    {
        protected MapLandPass(MapPlanContext context) => Context = context;

        protected MapPlanContext Context { get; }

        protected MapLand Land => Context.Land;

        protected MapClimate Climate => Context.Climate;

        protected MapSymmetry Symmetry => Context.Layout.Symmetry;

        public abstract void Plan();

        protected static float Hectares(int cells) => cells * MapLand.CellSize * MapLand.CellSize / 10000f;

        /// <summary>
        /// Cells of the authored sector per region whose surface is <paramref name="wanted"/>; with
        /// <paramref name="playableOnly"/> the backdrop outside the play area is left out.
        /// </summary>
        protected Dictionary<int, List<int>> SectorCells(Func<MapClimateSurface, bool> wanted, bool playableOnly)
        {
            var playArea = Context.Layout.Bounds;
            var cells = new Dictionary<int, List<int>>();
            var regionWanted = new bool[Land.Regions.Count];
            for (var i = 1; i < Land.Regions.Count; i++)
            {
                regionWanted[i] = wanted(Climate.Surface(Land.Regions[i].Surface));
            }

            Land.ForEach((x, z, p) =>
            {
                var region = Land.RegionIndex(x, z);
                var inArea = !playableOnly || playArea.Contains(p);
                if (!regionWanted[region] || !inArea || !Symmetry.InSector(p))
                {
                    return;
                }

                if (!cells.TryGetValue(region, out var list))
                {
                    cells[region] = list = new List<int>();
                }

                list.Add(z * Land.Width + x);
            });
            return cells;
        }

        /// <summary>A random point inside a random cell of the list.</summary>
        protected Vector2 RandomSpot(List<int> cells, System.Random random)
        {
            var cell = cells[random.Next(cells.Count)];
            var jitter = new Vector2(random.Value() - 0.5f, random.Value() - 0.5f);
            return Land.Center(cell % Land.Width, cell / Land.Width) + jitter * MapLand.CellSize;
        }
    }
}
