using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Singleton map of the ground's <see cref="SurfaceMaterial"/>, baked from the map's ground class image.</summary>
    public struct GroundMaterials : IComponentData
    {
        public BlobAssetReference<GroundMaterialsBlob> Blob;

        /// <summary>The material of the cell under a point; <see cref="SurfaceMaterial.None"/> off the map.</summary>
        public readonly SurfaceMaterial At(float2 position)
        {
            ref var map = ref Blob.Value;
            var cell = (int2)math.floor((position - map.Min) / map.Spacing);
            var outside = math.any(cell < 0) || math.any(cell >= map.Size);
            return outside ? SurfaceMaterial.None : (SurfaceMaterial)map.Cells[cell.y * map.Size.x + cell.x];
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

            root.Size = size;
            root.Min = min;
            root.Spacing = spacing;
            var blob = builder.CreateBlobAssetReference<GroundMaterialsBlob>(allocator);
            builder.Dispose();
            return new GroundMaterials { Blob = blob };
        }
    }

    /// <summary>Row-major cells from <see cref="Min"/> (the map's south-west corner), <see cref="Spacing"/> metres apart.</summary>
    public struct GroundMaterialsBlob
    {
        public BlobArray<byte> Cells;
        public int2 Size;
        public float2 Min;
        public float Spacing;
    }
}
