using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Faction number to player entity, rebuilt per frame so jobs can find an owner's stockpile.</summary>
    public static class PlayerLookup
    {
        /// <summary>One slot per possible <see cref="Faction.Value"/>.</summary>
        public const int Capacity = 256;

        /// <summary><paramref name="players"/> must match <see cref="Player"/>; unused slots hold <c>Entity.Null</c>.</summary>
        public static NativeArray<Entity> ByFaction(EntityQuery players, AllocatorManager.AllocatorHandle allocator)
        {
            var result = CollectionHelper.CreateNativeArray<Entity>(Capacity, allocator);
            var entities = players.ToEntityArray(Allocator.Temp);
            var data = players.ToComponentDataArray<Player>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                result[data[i].Faction] = entities[i];
            }

            return result;
        }
    }
}
