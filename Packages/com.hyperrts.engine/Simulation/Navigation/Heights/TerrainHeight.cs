using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Heights
{
    /// <summary>Singleton ground heightfield baked from a Terrain. Without one the ground is flat at y = 0.</summary>
    public struct TerrainHeight : IComponentData
    {
        // A crossing can run far along a steep ray, so the bisection halves it often.
        private const int RefineSteps = 16;

        public BlobAssetReference<HeightfieldBlob> Blob;

        public readonly bool IsCreated => Blob.IsCreated;

        /// <summary>Bilinear ground height, clamped to the edge samples outside the heightfield.</summary>
        public readonly float Height(float2 position)
        {
            if (!Blob.IsCreated)
            {
                return 0f;
            }

            ref var field = ref Blob.Value;
            var size = field.Frame.Size;
            var local = math.clamp(field.Frame.Local(position), 0f, size - 1);
            var cell = math.min((int2)local, size - 2);
            var t = local - cell;
            var row = field.Frame.Index(cell);
            var bottom = math.lerp(field.Heights[row], field.Heights[row + 1], t.x);
            var top = math.lerp(field.Heights[row + size.x], field.Heights[row + size.x + 1], t.x);
            return math.lerp(bottom, top, t.y);
        }

        /// <summary>
        /// First point where a ray dips below the ground, tested where it leaves each cell between samples then
        /// bisected; without a heightfield, where it meets the y = 0 plane.
        /// </summary>
        public readonly bool Raycast(float3 origin, float3 direction, float maxDistance, out float3 point)
        {
            point = default;
            if (!Blob.IsCreated)
            {
                return RaycastPlane(origin, direction, 0f, maxDistance, out point);
            }

            var end = origin + direction * maxDistance;
            var ray = new GridRay(Blob.Value.Frame, origin.xz, end.xz);
            while (ray.MoveNext())
            {
                if (IsBelow(math.lerp(origin, end, ray.Exit)))
                {
                    point = Refine(origin, direction, ray.Entry * maxDistance, ray.Exit * maxDistance);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Where a ray meets the horizontal plane at <paramref name="height"/>; false when it never does.</summary>
        public static bool RaycastPlane(float3 origin, float3 direction, float height, out float3 point) =>
            RaycastPlane(origin, direction, height, float.PositiveInfinity, out point);

        public static TerrainHeight Create(NativeArray<float> heights, int2 size, float2 min, float spacing,
            Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<HeightfieldBlob>();
            var samples = builder.Allocate(ref root.Heights, heights.Length);
            for (var i = 0; i < heights.Length; i++)
            {
                samples[i] = heights[i];
            }

            root.Frame = new GridFrame(min, spacing, size);
            var blob = builder.CreateBlobAssetReference<HeightfieldBlob>(allocator);
            builder.Dispose();
            return new TerrainHeight { Blob = blob };
        }

        private static bool RaycastPlane(float3 origin, float3 direction, float height, float maxDistance,
            out float3 point)
        {
            point = default;
            if (math.abs(direction.y) < 1e-5f)
            {
                return false;
            }

            var distance = (height - origin.y) / direction.y;
            if (distance < 0f || distance > maxDistance)
            {
                return false;
            }

            point = origin + direction * distance;
            point.y = height;
            return true;
        }

        private readonly bool IsBelow(float3 point) => point.y <= Height(point.xz);

        private readonly float3 Refine(float3 origin, float3 direction, float above, float below)
        {
            for (var i = 0; i < RefineSteps; i++)
            {
                var middle = (above + below) * 0.5f;
                if (IsBelow(origin + direction * middle))
                {
                    below = middle;
                }
                else
                {
                    above = middle;
                }
            }

            var point = origin + direction * below;
            return new float3(point.x, Height(point.xz), point.z);
        }
    }
}
