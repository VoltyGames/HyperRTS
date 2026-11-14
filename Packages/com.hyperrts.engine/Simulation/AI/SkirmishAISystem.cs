using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Skirmish AI for <see cref="AIPlayer"/>s: builds, produces, harvests, uses abilities, manages squads and attacks
    /// along the map's lanes (see <see cref="AIMapHints"/>). It only issues <see cref="PlayerCommand"/>s, like a human
    /// player. This system picks who thinks and in which order the planners run; one turn shares its busy units and
    /// its budget across them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct SkirmishAISystem : ISystem
    {
        private AIQueries _queries;
        private BuilderPlanner _builders;
        private BuildOrderPlanner _buildOrder;
        private EconomyPlanner _economy;
        private SquadPlanner _squads;
        private FieldWorkPlanner _fieldWorks;
        private DeployPlanner _deploy;
        private FireMissionPlanner _fireMissions;
        private AttackPlanner _attack;
        private AbilityPlanner _abilities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _queries = AIQueries.Create(ref state);
            _builders = BuilderPlanner.Create(ref state);
            _buildOrder = BuildOrderPlanner.Create(ref state);
            _economy = EconomyPlanner.Create(ref state);
            _squads = SquadPlanner.Create(ref state);
            _fieldWorks = FieldWorkPlanner.Create(ref state);
            _deploy = DeployPlanner.Create(ref state);
            _fireMissions = FireMissionPlanner.Create(ref state);
            _attack = AttackPlanner.Create(ref state);
            _abilities = AbilityPlanner.Create(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();

            // Without the map's hints there is no AI; AIMapHintsCheckSystem says so.
            state.RequireForUpdate<AIMapHints>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var due = DuePlayers(ref state);
            if (due.Length == 0)
            {
                return;
            }

            state.CompleteDependency();
            UpdatePlanners(ref state);
            var snapshot = AISnapshot.Take(ref state, _queries);
            foreach (var player in due)
            {
                Think(ref state, player, snapshot);
            }
        }

        private NativeList<Entity> DuePlayers(ref SystemState state)
        {
            var due = new NativeList<Entity>(Allocator.Temp);
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (ai, entity) in SystemAPI.Query<RefRW<AIPlayer>>().WithNone<Defeated>().WithEntityAccess())
            {
                ai.ValueRW.TimeUntilThink -= deltaTime;
                if (ai.ValueRO.TimeUntilThink <= 0f)
                {
                    ai.ValueRW.TimeUntilThink = ai.ValueRO.ThinkInterval;
                    due.Add(entity);
                }
            }

            return due;
        }

        private void UpdatePlanners(ref SystemState state)
        {
            _builders.Update();
            _squads.Update();
            _economy.Update();
            _buildOrder.Update(_builders);
            _fieldWorks.Update(_builders, _squads);
            _deploy.Update();
            _fireMissions.Update(ref state);
            _attack.Update(ref state, _squads);
            _abilities.Update(ref state);
        }

        private void Think(ref SystemState state, Entity player, in AISnapshot snapshot)
        {
            var turn = AITurn.Begin(ref state, _queries, player, snapshot);
            var buildOrderDone = _buildOrder.Plan(ref state, turn, snapshot);
            _builders.ResumeSites(ref state, turn);
            _squads.Plan(ref state, turn, snapshot);
            _economy.SendHarvesters(turn, snapshot);
            if (buildOrderDone)
            {
                _economy.Train(ref state, turn, snapshot);
                _fieldWorks.Plan(ref state, turn, snapshot);
            }

            _fireMissions.Plan(ref state, turn, snapshot);
            _deploy.Plan(ref state, turn);
            _attack.Plan(ref state, turn, snapshot);
            if (turn.Settings.UseAbilities)
            {
                _abilities.Plan(ref state, turn, snapshot);
            }
        }
    }
}
