using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>A straight run in from the owner's map edge, over a drop point and off the far side of the map.</summary>
    public struct DropRoute
    {
        /// <summary>How near the drop point counts as over it; an aircraft covers about a metre a frame.</summary>
        public const float DropRange = 4f;

        /// <summary>The aircraft heads this far past the edge, so it leaves the map before it despawns.</summary>
        private const float ExitMargin = 30f;

        public float3 Entry;
        public float3 Target;
        public float3 Exit;

        /// <summary>Enters on the edge nearest <paramref name="ownerStart"/>, <paramref name="altitude"/> above the target.</summary>
        public static DropRoute Plan(in MapSettings map, float3 ownerStart, float3 target, float altitude)
        {
            var entry = map.NearestEdgePoint(ownerStart);
            var inward = math.normalizesafe(map.Center.xz - entry.xz, new float2(0f, 1f));
            var heading = math.normalizesafe(target.xz - entry.xz, inward);
            var beyond = map.DistanceToEdge(target, heading) + ExitMargin;
            var cruise = target.y + altitude;
            return new DropRoute
            {
                Entry = new float3(entry.x, cruise, entry.z),
                Target = target,
                Exit = new float3(target.x + heading.x * beyond, cruise, target.z + heading.y * beyond),
            };
        }

        public readonly float2 Heading => math.normalizesafe(Exit.xz - Target.xz);

        public readonly quaternion Facing => quaternion.LookRotationSafe(new float3(Heading.x, 0f, Heading.y), math.up());

        public readonly float DistanceToTarget(float3 position) => math.distance(position.xz, Target.xz);

        public readonly bool IsOver(float3 position) => DistanceToTarget(position) <= DropRange;

        /// <summary>The drop point, or the exit once the aircraft is leaving.</summary>
        public readonly float3 Destination(bool leaving) => leaving ? Exit : Target;
    }
}
