using HyperRTS.Simulation.Match;
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
    }
}
