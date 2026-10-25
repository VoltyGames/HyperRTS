using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>
    /// Where buildings stand, as an R8 picture over the map at nav-cell resolution, published as
    /// <c>_HyperRTSFootprints</c> so grass and row crops skip those spots whatever the interaction-map quality.
    /// </summary>
    public sealed class BuildingFootprintMask : IDisposable
    {
        public static readonly int TextureId = Shader.PropertyToID("_HyperRTSFootprints");
        public static readonly int RectId = Shader.PropertyToID("_HyperRTSFootprintRect");

        // Cells within half a cell of a wall count, so plants at its foot stay hidden too.
        private const float Inset = 0.5f;

        private readonly Texture2D _texture;
        private readonly byte[] _cells;
        private readonly MapSettings _map;
        private readonly int2 _size;

        public BuildingFootprintMask(in MapSettings map)
        {
            _map = map;
            _size = math.max(1, (int2)math.ceil(map.Size / map.NavCellSize));
            _cells = new byte[_size.x * _size.y];
            _texture = new Texture2D(_size.x, _size.y, TextureFormat.R8, false, true)
            {
                name = "HyperRTS Building Footprints",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>Bumped whenever the published mask changes, so scattered plants know to regrow.</summary>
        [AutoStaticsCleanup]
        public static int Version { get; private set; }

        public bool Fits(in MapSettings map) => map.SameArea(_map) && map.NavCellSize == _map.NavCellSize;

        /// <summary>Redraws the mask from every building's turned box and publishes it.</summary>
        public void Draw(NativeArray<NavBox> boxes)
        {
            Array.Clear(_cells, 0, _cells.Length);
            foreach (var box in boxes)
            {
                Stamp(box);
            }

            _texture.SetPixelData(_cells, 0);
            _texture.Apply(false);
            Shader.SetGlobalTexture(TextureId, _texture);
            Shader.SetGlobalVector(RectId, InteractionMath.Rect(_map.Min, (float2)_size * _map.NavCellSize));
            Version++;
        }

        public bool IsMarked(float2 position)
        {
            var cell = (int2)math.floor((position - _map.Min) / _map.NavCellSize);
            var inside = math.all(cell >= 0 & cell < _size);
            return inside && _cells[cell.y * _size.x + cell.x] != 0;
        }

        /// <summary>No mask: shaders read a zero rect as no buildings.</summary>
        public void Dispose()
        {
            Shader.SetGlobalTexture(TextureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, Vector4.zero);
            Version++;
            Object.DestroyImmediate(_texture);
        }

        /// <summary>Copies the published mask to a compute kernel: compute shaders don't see globals.</summary>
        public static void Bind(ComputeShader compute, int kernel)
        {
            var mask = Shader.GetGlobalTexture(TextureId);
            var rect = Shader.GetGlobalVector(RectId);
            var on = mask != null && rect.z > 0f;
            compute.SetTexture(kernel, TextureId, on ? mask : Texture2D.blackTexture);
            compute.SetVector(RectId, on ? rect : Vector4.zero);
        }

        private void Stamp(in NavBox box)
        {
            var cellSize = _map.NavCellSize;
            var reach = box.Extents + cellSize * Inset;
            var low = math.max(0, (int2)math.floor((box.Center - reach - _map.Min) / cellSize));
            var high = math.min(_size - 1, (int2)math.floor((box.Center + reach - _map.Min) / cellSize));
            for (var z = low.y; z <= high.y; z++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    var center = _map.Min + (new float2(x, z) + 0.5f) * cellSize;
                    if (box.Gap(center) <= cellSize * Inset)
                    {
                        _cells[z * _size.x + x] = byte.MaxValue;
                    }
                }
            }
        }
    }
}
