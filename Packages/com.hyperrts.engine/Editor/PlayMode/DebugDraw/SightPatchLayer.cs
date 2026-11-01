using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>A selected unit's sight patch: green where it sees the ground, blue where only a tall target shows, yellow within close range.</summary>
    public sealed class SightPatchLayer : CellTextureLayer
    {
        public override string Label => "Sight patch (selected unit)";

        // Selection only exists in the world the player looks at.
        public override WorldSystemFilterFlags Source => SimulationWorlds.Presented;

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out FogOfWar fog) || !fog.IsCreated)
            {
                return false;
            }

            using var query = entityManager.CreateEntityQuery(typeof(SightPatch), typeof(SightPatchBits), typeof(Selected));
            using var units = query.ToEntityArray(Allocator.Temp);
            if (units.Length == 0)
            {
                return false;
            }

            var patch = entityManager.GetComponentData<SightPatch>(units[0]);
            if (patch.Radius < 0)
            {
                return false;
            }

            var frame = new GridFrame(fog.Frame.Min + (float2)patch.Origin * fog.Frame.CellSize, fog.Frame.CellSize, new int2(patch.Width));
            var bits = entityManager.GetBuffer<SightPatchBits>(units[0], true).Reinterpret<ulong>().AsNativeArray();
            texture.Paint(frame, new Colors { Patch = patch, Bits = bits });
            return true;
        }

        private struct Colors : ICellColors
        {
            public SightPatch Patch;
            [ReadOnly] public NativeArray<ulong> Bits;

            public Color32 At(int index)
            {
                var x = index % Patch.Width;
                var y = index / Patch.Width;
                if (Has(SightPatch.Ground, x, y))
                {
                    return new Color32(77, 255, 102, 110);
                }

                if (Has(SightPatch.Tall, x, y))
                {
                    return new Color32(80, 170, 255, 110);
                }

                return Has(SightPatch.Close, x, y) ? new Color32(255, 220, 60, 90) : default;
            }

            private bool Has(int layer, int x, int y) => (Bits[Patch.Word(layer, y, x >> 6)] & (1ul << (x & 63))) != 0;
        }
    }
}
