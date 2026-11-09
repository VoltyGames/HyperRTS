using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers.Vision
{
    /// <summary>A selected unit's sight area: green where it sees the ground, blue where only a tall target shows, yellow within close range.</summary>
    public sealed class SightAreaLayer : DebugLayer
    {
        public override string Label => "Sight area";

        public override string Detail => "The first selected unit's sight: green sees the ground, blue only tall targets, yellow close range.";

        public override string Group => "Vision";

        public override string Glyph => "scan-eye";

        // Selection only exists in the world the player looks at.
        public override WorldSystemFilterFlags Source => SimulationWorlds.Presented;

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out FogOfWar fog) || !fog.IsCreated)
            {
                return;
            }

            using var query = entityManager.CreateEntityQuery(typeof(SightArea), typeof(SightAreaBits), typeof(Selected));
            using var units = query.ToEntityArray(Allocator.Temp);
            if (units.Length == 0)
            {
                return;
            }

            var area = entityManager.GetComponentData<SightArea>(units[0]);
            if (area.Radius < 0)
            {
                return;
            }

            var grid = new CellGrid(fog.Grid.Min + (float2)area.Origin * fog.Grid.CellSize, fog.Grid.CellSize, new int2(area.Width));
            var bits = entityManager.GetBuffer<SightAreaBits>(units[0], true).Reinterpret<ulong>().AsNativeArray();
            shapes.Cells(grid, new Colors { Area = area, Bits = bits });
        }

        private struct Colors : ICellColors
        {
            public SightArea Area;
            [ReadOnly] public NativeArray<ulong> Bits;

            public Color32 At(int index)
            {
                var x = index % Area.Width;
                var y = index / Area.Width;
                if (Has(SightArea.Ground, x, y))
                {
                    return new Color32(77, 255, 102, 110);
                }

                if (Has(SightArea.Tall, x, y))
                {
                    return new Color32(80, 170, 255, 110);
                }

                return Has(SightArea.Close, x, y) ? new Color32(255, 220, 60, 90) : default;
            }

            private bool Has(int layer, int x, int y) => (Bits[Area.Word(layer, y, x >> 6)] & (1ul << (x & 63))) != 0;
        }
    }
}
