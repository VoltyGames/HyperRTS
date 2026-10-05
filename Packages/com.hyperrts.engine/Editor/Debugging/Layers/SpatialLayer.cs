using HyperRTS.Simulation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Debugging.Layers
{
    /// <summary>Spatial index cells that hold at least one entity.</summary>
    public sealed class SpatialLayer : CellDebugLayer
    {
        public override string Label => "Spatial cells";

        public override void Draw(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out SpatialIndex index) || !index.Cells.IsCreated)
            {
                return;
            }

            var (keys, count) = index.Cells.GetUniqueKeyArray(Allocator.Temp);
            BeginCells(new Color(0.3f, 0.6f, 1f, 0.25f));
            for (var i = 0; i < count; i++)
            {
                Cell((float2)SpatialIndex.CellOfKey(keys[i]) * index.CellSize, index.CellSize);
            }

            EndCells();
            keys.Dispose();
        }
    }
}
