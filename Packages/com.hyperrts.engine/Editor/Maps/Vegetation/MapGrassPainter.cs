using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Vegetation
{
    /// <summary>
    /// Grass, crops and small plants from the land use, with wild ground in noisy clumps and clearings and crops
    /// even. Nothing grows on roads, tramlines, deep water or placed objects.
    /// </summary>
    public static class MapGrassPainter
    {
        public const float PropGrassFalloff = 1.5f;

        /// <summary>Coverage per climate plant over the terrain, <paramref name="resolution"/> texels a side.</summary>
        public static MapGrassBake Paint(MapLand land, MapEdgeField edges, MapShapes shapes, MapHeights heights,
            MapClearance clearance, int resolution)
        {
            var bake = new MapGrassBake(shapes.Layout.TerrainBounds, resolution, shapes.Climate.plants.Count);
            var pass = new MapGrassPass(land, edges, shapes, heights, clearance, bake);
            MapParallel.ForRows(resolution, z =>
            {
                for (var x = 0; x < resolution; x++)
                {
                    pass.PaintCell(x, z);
                }
            });

            return bake;
        }

        /// <summary>Grass share near placed objects: none on a footprint, full past <see cref="PropGrassFalloff"/>.</summary>
        public static float GrassNearProps(MapClearance clearance, Vector2 point)
        {
            var edge = clearance.ReservedEdgeDistance(point, PropGrassFalloff);
            return float.IsPositiveInfinity(edge) ? 1f : MapGeometry.Smooth(edge / PropGrassFalloff);
        }
    }
}
