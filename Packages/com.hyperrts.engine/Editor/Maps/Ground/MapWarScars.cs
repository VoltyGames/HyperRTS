using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// The old craters and burnt patches a war age left, copies included. Planning digs the craters; painting burns
    /// the ground and grows the plants back round them.
    /// </summary>
    public sealed class MapWarScars
    {
        private readonly MapNoise _edges;

        public MapWarScars(MapNoise edges) => _edges = edges;

        public List<MapWarCrater> Craters { get; } = new();

        public List<MapBurntPatch> Burns { get; } = new();

        public void Dig(MapWarCrater crater, MapHeights heights)
        {
            crater.DigInto(heights);
            Craters.Add(crater);
        }

        /// <summary>Lays the climate's burnt layer over the texture weights ([z, x, layer] over <paramref name="area"/>).</summary>
        public void Burn(float[,,] textureWeights, Rect area, MapClimate climate)
        {
            if (Burns.Count == 0)
            {
                return;
            }

            var burnt = climate.LayerIndex(climate.war.burntLayer);
            var layers = textureWeights.GetLength(2);
            foreach (var patch in Burns)
            {
                ForEachTexel(area, textureWeights.GetLength(0), patch.Center, patch.Reach, (x, z, p) =>
                {
                    var cover = patch.Cover(p, _edges);
                    for (var l = 0; l < layers; l++)
                    {
                        textureWeights[z, x, l] *= 1f - cover;
                    }

                    textureWeights[z, x, burnt] += cover;
                });
            }
        }

        /// <summary>Thins the plants in each bowl and grows the war kit's weeds in a ring round it.</summary>
        public void Regrow(MapGrassBake grass, MapClimate climate)
        {
            if (Craters.Count == 0)
            {
                return;
            }

            var kit = climate.war;
            var weeds = string.IsNullOrEmpty(kit.weedPlant) ? null : grass.Layers[climate.PlantIndex(kit.weedPlant)];
            foreach (var crater in Craters)
            {
                ForEachTexel(grass.Area, grass.Resolution, crater.Center, crater.Radius + kit.weedRing, (x, z, p) =>
                {
                    var i = z * grass.Resolution + x;
                    var distance = Vector2.Distance(p, crater.Center);
                    var kept = crater.Plants(distance, kit.bowlPlants);
                    foreach (var layer in grass.Layers)
                    {
                        layer[i] = (byte)Mathf.RoundToInt(layer[i] * kept);
                    }

                    if (weeds != null)
                    {
                        var cover = crater.Weeds(distance, kit.weedRing) * kit.weedCoverage * 255f;
                        weeds[i] = (byte)Mathf.Max(weeds[i], Mathf.RoundToInt(cover));
                    }
                });
            }
        }

        /// <summary>Calls back for every texel of a square grid over <paramref name="area"/> whose centre lies within reach.</summary>
        private static void ForEachTexel(Rect area, int resolution, Vector2 center, float reach, Action<int, int, Vector2> visit)
        {
            var texel = area.size / resolution;
            var min = Vector2Int.FloorToInt((center - Vector2.one * reach - area.min) / texel);
            var max = Vector2Int.CeilToInt((center + Vector2.one * reach - area.min) / texel);
            for (var z = Mathf.Max(0, min.y); z <= Mathf.Min(resolution - 1, max.y); z++)
            {
                for (var x = Mathf.Max(0, min.x); x <= Mathf.Min(resolution - 1, max.x); x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), texel);
                    if ((p - center).sqrMagnitude <= reach * reach)
                    {
                        visit(x, z, p);
                    }
                }
            }
        }
    }
}
