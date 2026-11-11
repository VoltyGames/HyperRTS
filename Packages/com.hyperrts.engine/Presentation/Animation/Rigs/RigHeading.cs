using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>Where a rigged root stands and which way it faces along the ground.</summary>
    internal readonly struct RigHeading
    {
        public readonly float3 Position;
        public readonly float3 Forward;
        public readonly float3 Right;

        public RigHeading(in LocalTransform place)
        {
            var facing = place.Forward();
            Position = place.Position;
            Forward = math.normalizesafe(new float3(facing.x, 0f, facing.z), math.forward());
            Right = math.cross(math.up(), Forward);
        }
    }
}
