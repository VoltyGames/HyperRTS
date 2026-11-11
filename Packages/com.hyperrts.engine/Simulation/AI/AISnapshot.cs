using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Query results shared by every AI that thinks this frame.</summary>
    internal struct AISnapshot
    {
        public AIGroup Targets;
        public AIGroup Buildings;
        public AIGroup Owned;
        public NativeArray<EntityInfo> OwnedInfo;
        public NativeArray<Entity> Producers;
        public CompletedBuildings Completed;
        public OwnerRelations Relations;
        public MapSettings Map;
        public NavGrid Grid;
        public AIMapHintsView Hints;

        public static AISnapshot Take(ref SystemState state, in AIQueries queries)
        {
            var hints = queries.Hints.GetSingletonEntity();
            var entities = state.EntityManager;
            return new AISnapshot
            {
                Targets = AIGroup.From(queries.Targets),
                Buildings = AIGroup.From(queries.Buildings),
                Owned = AIGroup.From(queries.Owned),
                OwnedInfo = queries.Owned.ToComponentDataArray<EntityInfo>(Allocator.Temp),
                Producers = queries.Producers.ToEntityArray(Allocator.Temp),
                Completed = new CompletedBuildings(queries.Completed, Allocator.Temp),
                Relations = queries.Relations.GetSingleton<OwnerRelations>(),
                Map = queries.Map.GetSingleton<MapSettings>(),
                Grid = queries.Grid.GetSingleton<NavGrid>(),
                Hints = new AIMapHintsView(entities.GetBuffer<AISlotHint>(hints),
                    entities.GetBuffer<AILaneHint>(hints), entities.GetBuffer<AIHintPoint>(hints)),
            };
        }

        /// <summary>Where the AI builds around: its building with the lowest entity index, else its first unit.</summary>
        public readonly float3 FindHome(byte owner)
        {
            var home = FirstOwned(Buildings, owner);
            if (home >= 0)
            {
                return Buildings.Position(home);
            }

            var first = FirstOwned(Owned, owner);
            return first >= 0 ? Owned.Position(first) : float3.zero;
        }

        private static int FirstOwned(in AIGroup group, byte owner)
        {
            var best = -1;
            for (var i = 0; i < group.Length; i++)
            {
                if (!group.IsOwnedBy(i, owner))
                {
                    continue;
                }

                if (best < 0 || group.Entities[i].Index < group.Entities[best].Index)
                {
                    best = i;
                }
            }

            return best;
        }
    }
}
