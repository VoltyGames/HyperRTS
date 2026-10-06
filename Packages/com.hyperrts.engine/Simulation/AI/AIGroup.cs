using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Entities, owners and positions copied from one query for the AI's searches.</summary>
    public struct AIGroup
    {
        public NativeArray<Entity> Entities;
        public NativeArray<Faction> Owners;
        public NativeArray<LocalTransform> Transforms;

        public readonly int Length => Entities.Length;

        /// <summary><paramref name="query"/> must include <see cref="Faction"/> and <see cref="LocalTransform"/>.</summary>
        public static AIGroup From(EntityQuery query) => new()
        {
            Entities = query.ToEntityArray(Allocator.Temp),
            Owners = query.ToComponentDataArray<Faction>(Allocator.Temp),
            Transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp),
        };

        public readonly float3 Position(int index) => Transforms[index].Position;

        public readonly bool IsOwnedBy(int index, byte faction) => Owners[index].Value == faction;

        /// <summary>Squared distance on the ground plane, the measure every AI search ranks by.</summary>
        public readonly float DistanceSq(int index, float3 from) => math.distancesq(Position(index).xz, from.xz);

        /// <summary>Index of the entity owned by <paramref name="faction"/> nearest to a point, or -1.</summary>
        public readonly int NearestOwned(byte faction, float3 from)
        {
            var closest = Closest.None;
            for (var i = 0; i < Length; i++)
            {
                if (IsOwnedBy(i, faction))
                {
                    closest.Offer(i, Entities[i], DistanceSq(i, from));
                }
            }

            return closest.Index;
        }
    }
}
