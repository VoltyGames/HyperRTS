using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues.Surfaces
{
    /// <summary>Singleton map of the ground's <see cref="SurfaceMaterial"/>, baked from the map's <see cref="GroundMaterialMap"/>.</summary>
    public struct GroundMaterials : IComponentData
    {
        public BlobAssetReference<GroundMaterialsBlob> Blob;

        /// <summary>The material of the cell under a point; <see cref="SurfaceMaterial.None"/> off the map.</summary>
        public readonly SurfaceMaterial At(float2 position)
        {
            ref var map = ref Blob.Value;
            var cell = map.Grid.Cell(position);
            return map.Grid.Contains(cell) ? (SurfaceMaterial)map.Cells[map.Grid.Index(cell)] : SurfaceMaterial.None;
        }

        public static GroundMaterials Create(NativeArray<byte> cells, int2 size, float2 min, float spacing,
            Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GroundMaterialsBlob>();
            var stored = builder.Allocate(ref root.Cells, cells.Length);
            for (var i = 0; i < cells.Length; i++)
            {
                stored[i] = cells[i];
            }

            root.Grid = new CellGrid(min, spacing, size);
            var blob = builder.CreateBlobAssetReference<GroundMaterialsBlob>(allocator);
            builder.Dispose();
            return new GroundMaterials { Blob = blob };
        }
    }

    /// <summary>Row-major cells on a <see cref="CellGrid"/> from the map's south-west corner.</summary>
    public struct GroundMaterialsBlob
    {
        public BlobArray<byte> Cells;
        public CellGrid Grid;
    }
}
