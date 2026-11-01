using System;
using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// Where buildings stand, as an R8 picture over the map at nav-cell resolution, published as
    /// <c>_HyperRTSFootprints</c> so grass and row crops skip those spots whatever the trample-map quality.
    /// </summary>
    public sealed partial class BuildingFootprintMask : IDisposable
    {
        public static readonly int TextureId = Shader.PropertyToID("_HyperRTSFootprints");
        public static readonly int RectId = Shader.PropertyToID("_HyperRTSFootprintRect");

        // Cells within half a cell of a wall count, so plants at its foot stay hidden too.
        private const float Inset = 0.5f;

        private readonly Texture2D _texture;
        private readonly byte[] _cells;
        private readonly MapSettings _map;
        private readonly GridFrame _frame;

        public BuildingFootprintMask(in MapSettings map)
        {
            _map = map;
            _frame = map.NavFrame();
            _cells = new byte[_frame.Count];
            _texture = new Texture2D(_frame.Size.x, _frame.Size.y, TextureFormat.R8, false, true)
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

        public bool Fits(in MapSettings map) => map.SameNavGrid(_map);

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
            Shader.SetGlobalVector(RectId, TerrainHeightInput.Rect(_frame.Min, (float2)_frame.Size * _frame.CellSize));
            Version++;
        }

        public bool IsMarked(float2 position)
        {
            var cell = _frame.Cell(position);
            return _frame.Contains(cell) && _cells[_frame.Index(cell)] != 0;
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
            var inset = _frame.CellSize * Inset;
            var reach = box.Extents + inset;
            foreach (var cell in _frame.Cells(_frame.Cell(box.Center - reach), _frame.Cell(box.Center + reach)))
            {
                if (box.Gap(_frame.Center(cell)) <= inset)
                {
                    _cells[_frame.Index(cell)] = byte.MaxValue;
                }
            }
        }
    }
}
