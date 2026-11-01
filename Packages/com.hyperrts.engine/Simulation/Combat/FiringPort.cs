using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A window or loophole on a container that its passengers fire out of, in the container's local space.</summary>
    [InternalBufferCapacity(0)]
    public struct FiringPort : IBufferElementData
    {
        public float3 Position;

        /// <summary>Unit vector pointing out of the container.</summary>
        public float3 Direction;

        /// <summary>
        /// The port facing <paramref name="target"/> best (nearest to it on a tie), placed in the world and turned to
        /// face it level. False when the container has no ports.
        /// </summary>
        public static bool TryChoose(DynamicBuffer<FiringPort> ports, in LocalTransform container, float3 target,
            out LocalTransform chosen)
        {
            chosen = default;
            if (ports.Length == 0)
            {
                return false;
            }

            var toward = math.normalizesafe(target - container.Position);
            var bestFacing = float.MinValue;
            var bestGap = float.MaxValue;
            var best = 0;
            for (var i = 0; i < ports.Length; i++)
            {
                var facing = math.dot(math.rotate(container.Rotation, ports[i].Direction), toward);
                var gap = math.distancesq(container.TransformPoint(ports[i].Position), target);
                var sameFacing = math.abs(facing - bestFacing) <= 1e-4f;
                if (facing > bestFacing + 1e-4f || (sameFacing && gap < bestGap))
                {
                    bestFacing = facing;
                    bestGap = gap;
                    best = i;
                }
            }

            var position = container.TransformPoint(ports[best].Position);
            var level = target - position;
            level.y = 0f;
            chosen = LocalTransform.FromPositionRotation(position, quaternion.LookRotationSafe(level, math.up()));
            return true;
        }
    }
}
