using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Grass;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Painted plant coverage, a byte layer per climate plant over the terrain, and its per-tile summary.</summary>
    public sealed class MapGrassBake
    {
        public MapGrassBake(Rect area, int resolution, int plants)
        {
            Area = area;
            Resolution = resolution;
            Layers = new byte[plants][];
            for (var p = 0; p < plants; p++)
            {
                Layers[p] = new byte[resolution * resolution];
            }
        }

        /// <summary>World XZ rectangle the layers cover.</summary>
        public Rect Area { get; }

        /// <summary>Texels per side of every layer.</summary>
        public int Resolution { get; }

        /// <summary>Coverage per plant in climate order, 0 to 255, row by row from the minimum corner.</summary>
        public byte[][] Layers { get; }

        public bool IsEmpty => Layers.Length == 0;

        public void Set(int plant, int x, int z, float coverage) =>
            Layers[plant][z * Resolution + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);

        /// <summary>Every <see cref="GrassField.TileSize"/> tile that grows any plant, with each plant's mean coverage there.</summary>
        public (List<GrassTile> Tiles, List<GrassTilePlant> Plants) Tiles()
        {
            var across = Mathf.CeilToInt(Area.width / GrassField.TileSize);
            var deep = Mathf.CeilToInt(Area.height / GrassField.TileSize);
            var (sums, texels) = TileSums(across, deep);
            var (tiles, plants) = (new List<GrassTile>(), new List<GrassTilePlant>());
            for (var t = 0; t < across * deep; t++)
            {
                var first = plants.Count;
                for (var p = 0; p < Layers.Length; p++)
                {
                    if (sums[p][t] > 0f)
                    {
                        plants.Add(new GrassTilePlant { plant = p, coverage = sums[p][t] / texels[t] });
                    }
                }

                if (plants.Count > first)
                {
                    tiles.Add(new GrassTile { x = t % across, z = t / across, firstPlant = first, plantCount = plants.Count - first });
                }
            }

            return (tiles, plants);
        }

        /// <summary>Summed coverage per plant and texel count per tile, each texel in the tile its centre falls in.</summary>
        private (float[][] Sums, int[] Texels) TileSums(int across, int deep)
        {
            var sums = new float[Layers.Length][];
            for (var p = 0; p < Layers.Length; p++)
            {
                sums[p] = new float[across * deep];
            }

            var texels = new int[across * deep];
            var cell = Area.size / Resolution;
            for (var i = 0; i < Resolution * Resolution; i++)
            {
                var (x, z) = (i % Resolution, i / Resolution);
                var tileX = Mathf.Min(across - 1, Mathf.FloorToInt((x + 0.5f) * cell.x / GrassField.TileSize));
                var tileZ = Mathf.Min(deep - 1, Mathf.FloorToInt((z + 0.5f) * cell.y / GrassField.TileSize));
                var tile = tileZ * across + tileX;
                texels[tile]++;
                for (var p = 0; p < Layers.Length; p++)
                {
                    sums[p][tile] += Layers[p][i] / 255f;
                }
            }

            return (sums, texels);
        }
    }
}
