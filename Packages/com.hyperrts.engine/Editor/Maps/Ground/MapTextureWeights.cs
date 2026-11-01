using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Terrain layer weights over the terrain, one byte layer per climate layer row by row from the south, as the
    /// terrain's alphamaps keep them: a 2048 texel map of 16 layers takes 64 MB instead of 256.
    /// </summary>
    public sealed class MapTextureWeights
    {
        // Rows per SetAlphamaps call, so the float copy the terrain takes stays a few megabytes.
        private const int StripRows = 64;

        private readonly byte[][] _layers;

        public MapTextureWeights(int resolution, int layers)
        {
            Resolution = resolution;
            _layers = new byte[layers][];
            for (var l = 0; l < layers; l++)
            {
                _layers[l] = new byte[resolution * resolution];
            }
        }

        /// <summary>Texels per side.</summary>
        public int Resolution { get; }

        public int LayerCount => _layers.Length;

        /// <summary>A texel's weight of one layer, 0 to 1 in 255 steps.</summary>
        public float this[int x, int z, int layer]
        {
            get => _layers[layer][z * Resolution + x] / 255f;
            set => _layers[layer][z * Resolution + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        /// <summary>Sets the terrain's alphamaps to these weights, a strip of rows at a time.</summary>
        public void WriteTo(TerrainData data)
        {
            data.alphamapResolution = Resolution;
            var strip = new float[StripRows, Resolution, _layers.Length];
            for (var first = 0; first < Resolution; first += StripRows)
            {
                var rows = Mathf.Min(StripRows, Resolution - first);
                if (rows < StripRows)
                {
                    strip = new float[rows, Resolution, _layers.Length];
                }

                for (var row = 0; row < rows; row++)
                {
                    for (var x = 0; x < Resolution; x++)
                    {
                        for (var l = 0; l < _layers.Length; l++)
                        {
                            strip[row, x, l] = this[x, first + row, l];
                        }
                    }
                }

                data.SetAlphamaps(0, first, strip);
            }
        }
    }
}
