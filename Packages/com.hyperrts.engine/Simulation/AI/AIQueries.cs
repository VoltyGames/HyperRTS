using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources.Gathering;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>The queries behind <see cref="AISnapshot"/> and <see cref="AITurn"/>, made once in OnCreate.</summary>
    internal struct AIQueries
    {
        public EntityQuery Targets;
        public EntityQuery Buildings;
        public EntityQuery Owned;
        public EntityQuery Producers;
        public EntityQuery Completed;
        public EntityQuery Queues;
        public EntityQuery Limited;
        public EntityQuery Relations;
        public EntityQuery Map;
        public EntityQuery Grid;
        public EntityQuery Hints;

        public static AIQueries Create(ref SystemState state) => new()
        {
            Targets = new EntityQueryBuilder(Allocator.Temp).WithAll<Health, Owner, LocalTransform>()
                .WithNone<Dead>().Build(ref state),
            Buildings = new EntityQueryBuilder(Allocator.Temp).WithAll<BuildingTag, Owner, LocalTransform>()
                .WithNone<Dead>().Build(ref state),
            Owned = new EntityQueryBuilder(Allocator.Temp).WithAll<EntityInfo, Owner, LocalTransform>()
                .WithNone<Dead>().Build(ref state),
            Producers = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Producer, ProductionOption, ProductionQueueItem, Owner>().WithNone<Dead>().Build(ref state),
            Completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state),
            Queues = Buyer.QueueQuery(Allocator.Temp).Build(ref state),
            Limited = BuildCounts.Query(Allocator.Temp).Build(ref state),
            Relations = new EntityQueryBuilder(Allocator.Temp).WithAll<OwnerRelations>().Build(ref state),
            Map = new EntityQueryBuilder(Allocator.Temp).WithAll<MapSettings>().Build(ref state),
            Grid = new EntityQueryBuilder(Allocator.Temp).WithAll<NavGrid>().Build(ref state),
            Hints = new EntityQueryBuilder(Allocator.Temp).WithAll<AIMapHints>().Build(ref state),
        };
    }
}
