using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Singleton crop cover baked from the map's <see cref="CropCoverMap"/>; <see cref="SightBlockers"/> reads it.</summary>
    public struct CropCover : IComponentData
    {
        public BlobAssetReference<CropCoverBlob> Blob;

        public static CropCover Create(NativeArray<byte> density, NativeArray<byte> top, int2 size, float2 min,
            float spacing, Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CropCoverBlob>();
            var densities = builder.Allocate(ref root.Density, density.Length);
            var tops = builder.Allocate(ref root.Top, top.Length);
            for (var i = 0; i < density.Length; i++)
            {
                densities[i] = density[i];
                tops[i] = top[i];
            }

            root.Frame = new GridFrame(min, spacing, size);
            var blob = builder.CreateBlobAssetReference<CropCoverBlob>(allocator);
            builder.Dispose();
            return new CropCover { Blob = blob };
        }
    }

    /// <summary>Row-major cells on a <see cref="GridFrame"/> from the map's south-west corner.</summary>
    public struct CropCoverBlob
    {
        public BlobArray<byte> Density;
        public BlobArray<byte> Top;
        public GridFrame Frame;
    }
}
