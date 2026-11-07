using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>A selected unit's sight area: green where it sees the ground, blue where only a tall target shows, yellow within close range.</summary>
    public sealed class SightAreaLayer : CellTextureLayer
    {
        public override string Label => "Sight area";

        public override string Detail => "What the selected unit sees";

        public override string Group => "Vision";

        public override string Glyph => "scan-eye";

        // Selection only exists in the world the player looks at.
        public override WorldSystemFilterFlags Source => SimulationWorlds.Presented;

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out FogOfWar fog) || !fog.IsCreated)
            {
                return false;
            }

            using var query = entityManager.CreateEntityQuery(typeof(SightArea), typeof(SightAreaBits), typeof(Selected));
            using var units = query.ToEntityArray(Allocator.Temp);
            if (units.Length == 0)
            {
                return false;
            }

            var area = entityManager.GetComponentData<SightArea>(units[0]);
            if (area.Radius < 0)
            {
                return false;
            }

            var grid = new CellGrid(fog.Grid.Min + (float2)area.Origin * fog.Grid.CellSize, fog.Grid.CellSize, new int2(area.Width));
            var bits = entityManager.GetBuffer<SightAreaBits>(units[0], true).Reinterpret<ulong>().AsNativeArray();
            texture.Paint(grid, new Colors { Area = area, Bits = bits });
            return true;
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
