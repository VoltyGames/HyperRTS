using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Paints the ground the local player's selected units see, from their <see cref="SightArea"/>s, into a mask the
    /// fog pass outlines while <see cref="SightShapeView"/> is shown.
    /// </summary>
    public sealed class SightShape : IDisposable
    {
        private static readonly int MaskId = Shader.PropertyToID("_HyperRTSSightShape");
        private static readonly int ColorId = Shader.PropertyToID("_HyperRTSSightShapeColor");

        private readonly LiveSingleton<SightShapeView> _view = new();
        private readonly LiveQuery _selected = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<SightArea>(),
            ComponentType.ReadOnly<SightAreaBits>(), ComponentType.ReadOnly<Owner>()));
        private Texture2D _mask;

        /// <summary>Paints and publishes the outline, or hides it when not shown.</summary>
        public void Update(EntityManager entityManager, in FogOfWar fog, in LocalFogView view, Color color)
        {
            var shown = _view.TryGet(entityManager, out var shape) && shape.Shown;
            if (!shown)
            {
                Hide();
                return;
            }

            Paint(entityManager, fog, view.Viewer);
            Shader.SetGlobalTexture(MaskId, _mask);
            Shader.SetGlobalColor(ColorId, color);
        }

        // Zero alpha draws no outline.
        public static void Hide() => Shader.SetGlobalColor(ColorId, Color.clear);

        public void Dispose()
        {
            Hide();
            DestroyMask();
        }

        private void DestroyMask()
        {
            if (_mask != null)
            {
                Object.Destroy(_mask);
            }
        }

        private void Paint(EntityManager entityManager, in FogOfWar fog, byte viewer)
        {
            if (_mask == null || _mask.width != fog.Grid.Size.x || _mask.height != fog.Grid.Size.y)
            {
                DestroyMask();
                _mask = new Texture2D(fog.Grid.Size.x, fog.Grid.Size.y, TextureFormat.R8, false, true)
                {
                    name = "Sight Shape",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
            }

            var pixels = _mask.GetPixelData<byte>(0);
            ClearJob<byte>.Schedule(pixels, default).Complete();
            var query = _selected.In(entityManager);
            query.CompleteDependency();
            using var units = query.ToEntityArray(Allocator.Temp);
            foreach (var unit in units)
            {
                var area = entityManager.GetComponentData<SightArea>(unit);
                if (entityManager.GetComponentData<Owner>(unit).Value != viewer || area.Radius < 0)
                {
                    continue;
                }

                var words = entityManager.GetBuffer<SightAreaBits>(unit, true).Reinterpret<ulong>().AsNativeArray();
                new PaintJob { Pixels = pixels, Words = words, Area = area, Size = fog.Grid.Size }.Run();
            }

            _mask.Apply(false);
        }

        [BurstCompile]
        private struct PaintJob : IJob
        {
            public NativeArray<byte> Pixels;
            [ReadOnly] public NativeArray<ulong> Words;
            public SightArea Area;
            public int2 Size;

            public void Execute()
            {
                for (var row = 0; row < Area.Width; row++)
                {
                    for (var column = 0; column < Area.Width; column++)
                    {
                        var cell = Area.Origin + new int2(column, row);
                        var word = Words[Area.Word(SightArea.Ground, row, column >> 6)];
                        var inside = math.all(cell >= 0 & cell < Size);
                        if (inside && (word & (1ul << (column & 63))) != 0)
                        {
                            Pixels[cell.y * Size.x + cell.x] = byte.MaxValue;
                        }
                    }
                }
            }
        }
    }
}
