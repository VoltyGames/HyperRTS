using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Targeting;

/// <summary>
/// The singleton queries a <see cref="TargetLookup"/> reads. Systems create them in OnCreate because the lookup travels into jobs, which can't carry a query.
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

    /// <summary>The singletons, through <c>out</c> because Burst only lets a lookup's [ReadOnly] fields be filled that way.</summary>
    public readonly void Read(out FogOfWar fog, out SightRules sight, out SightBlockers blockers)
    {
        fog = Fog.GetSingleton<FogOfWar>();
        sight = Sight.GetSingleton<SightRules>();
        blockers = Blockers.GetSingleton<SightBlockers>();
    }
}
