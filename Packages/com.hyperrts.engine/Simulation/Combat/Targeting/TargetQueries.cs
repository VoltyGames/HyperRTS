using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// The singleton queries a <see cref="TargetLookup"/> reads each update. Systems create them in OnCreate and pass
    /// them in: the lookup travels into jobs, which can't carry a query, and a system asking for a query in OnUpdate
    /// pays a lookup each frame. Holding them also makes the system's jobs wait for the fog restamp.
    /// </summary>
    public struct TargetQueries
    {
        public EntityQuery Fog;
        public EntityQuery Sight;
        public EntityQuery Blockers;

        public static TargetQueries Create(ref SystemState state) => new()
        {
            Fog = new EntityQueryBuilder(Allocator.Temp).WithAll<FogOfWar>().Build(ref state),
            Sight = new EntityQueryBuilder(Allocator.Temp).WithAll<SightRules>().Build(ref state),
            Blockers = SightBlockers.Query(ref state),
        };
    }
}
