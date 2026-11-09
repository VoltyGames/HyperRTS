using HyperRTS.Simulation.Cues.Surfaces;
using System;
using HyperRTS.Presentation.Common;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Fire
{
    public sealed partial class FieldFires
    {
        // Rows of the alphamap read at once, so a large terrain never copies all its weights in one go.
        private const int AlphamapRows = 64;

        /// <summary>Marks dry every cell whose terrain is mostly <paramref name="dryLayers"/> and isn't water.</summary>
        public void MarkDryTerrain(Terrain terrain, TerrainLayer[] dryLayers, in GroundMaterials ground)
        {
            var data = terrain.terrainData;
            var resolution = data.alphamapResolution;
            var dry = DryTexels(data, dryLayers);
            var origin = ((float3)terrain.GetPosition()).xz;
            var perMetre = resolution / ((float3)data.size).xz;
            foreach (var cell in _grid.Cells(int2.zero, Size - 1))
            {
                var centre = _grid.Center(cell);
                var texel = (int2)math.floor((centre - origin) * perMetre);
                var onTerrain = math.all(texel >= 0) && math.all(texel < resolution);
                if (onTerrain && dry[texel.x + texel.y * resolution] && ground.At(centre) != SurfaceMaterial.Water)
                {
                    MarkDry(cell);
                }
            }
        }

        // Whether each alphamap texel is over half dry layers, row-major from the terrain's south-west corner.
        private static bool[] DryTexels(TerrainData data, TerrainLayer[] dryLayers)
        {
            var resolution = data.alphamapResolution;
            var burns = Array.ConvertAll(data.terrainLayers, layer => Array.IndexOf(dryLayers, layer) >= 0);
            var dry = new bool[resolution * resolution];
            for (var row = 0; row < resolution; row += AlphamapRows)
            {
                var rows = Math.Min(AlphamapRows, resolution - row);
                var weights = data.GetAlphamaps(0, row, resolution, rows);
                for (var z = 0; z < rows; z++)
                {
                    for (var x = 0; x < resolution; x++)
                    {
                        var share = 0f;
                        for (var layer = 0; layer < burns.Length; layer++)
                        {
                            share += burns[layer] ? weights[z, x, layer] : 0f;
                        }

                        dry[x + (row + z) * resolution] = share > 0.5f;
                    }
                }
            }

            return dry;
        }
    }
}
