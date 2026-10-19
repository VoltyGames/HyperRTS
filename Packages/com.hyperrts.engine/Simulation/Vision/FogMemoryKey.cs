using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Identity of a remembered root. A client gets a new entity when a ghost turns relevant again, so ghosts are known
    /// by ghost id and spawn tick, everything else by entity.
    /// </summary>
    public struct FogMemoryKey : IEquatable<FogMemoryKey>
    {
        public Entity Entity;
        public int GhostId;
        public uint SpawnTick;

        public static FogMemoryKey Identify(Entity entity, in ComponentLookup<GhostInstance> ghosts)
        {
            var replicated = ghosts.TryGetComponent(entity, out var ghost) && ghost.ghostId != 0;
            return replicated
                ? new FogMemoryKey { GhostId = ghost.ghostId, SpawnTick = ghost.spawnTick.SerializedData }
                : new FogMemoryKey { Entity = entity };
        }

        public bool Equals(FogMemoryKey other)
        {
            var sameGhost = GhostId == other.GhostId && SpawnTick == other.SpawnTick;
            return sameGhost && Entity == other.Entity;
        }

        public override bool Equals(object obj) => obj is FogMemoryKey other && Equals(other);

        public override int GetHashCode() => (int)math.hash(new int3(Entity.GetHashCode(), GhostId, (int)SpawnTick));
    }
}
