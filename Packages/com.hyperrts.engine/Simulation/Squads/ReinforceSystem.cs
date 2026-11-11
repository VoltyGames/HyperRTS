using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Handles the Reinforce command: squads with dead members in range of a friendly <see cref="ReinforcePoint"/> bring them back one at a time for <see cref="SquadRules.ReinforceCost"/> each.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct ReinforceSystem : ISystem
    {
        private CommandReader _commands;
        private EntityQuery _points;
        private SquadRules _rules;
        private OwnerRelations _relations;
        private SquadPoints _squadPoints;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<SquadMember, Selected, Owner>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Reinforce), selected);
            _points = SystemAPI.QueryBuilder().WithAll<ReinforcePoint, Owner, LocalTransform>()
                .WithNone<Dead, Offline>().Build();
            state.RequireForUpdate<SquadRules>();
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _rules = SystemAPI.GetSingleton<SquadRules>();
            _relations = SystemAPI.GetSingleton<OwnerRelations>();
            _squadPoints = SquadPoints.From<ReinforcePoint>(_points);
            foreach (var each in _commands.Read(ref state))
            {
                foreach (var squad in _commands.Subjects(ref state, each))
                {
                    Begin(ref state, squad, each.Player);
                }
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (reinforcing, enabled, members, health, entity) in SystemAPI
                         .Query<RefRW<Reinforcing>, EnabledRefRW<Reinforcing>, DynamicBuffer<SquadMember>, RefRW<Health>>()
                         .WithEntityAccess())
            {
                if (!Advance(ref state, entity, ref reinforcing.ValueRW, members, ref health.ValueRW, deltaTime))
                {
                    enabled.ValueRW = false;
                }
            }
        }

        private void Begin(ref SystemState state, Entity squad, Entity player)
        {
            if (!SystemAPI.HasComponent<Reinforcing>(squad) || SystemAPI.IsComponentEnabled<Reinforcing>(squad))
            {
                return;
            }

            var owner = SystemAPI.GetComponent<Owner>(squad).Value;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var point = _squadPoints.NearestCovering(here, owner, _relations);
            var refusal = SquadOrderRule.FindReinforceRefusal(SystemAPI.GetBuffer<SquadMember>(squad),
                point != Entity.Null, _rules, Cost(ref state, squad), SystemAPI.GetBuffer<ResourceStock>(player));
            if (refusal == SquadRefusal.None)
            {
                SystemAPI.SetComponent(squad, new Reinforcing { Point = point, Payer = player });
                SystemAPI.SetComponentEnabled<Reinforcing>(squad, true);
            }
        }

        private DynamicBuffer<ResourceCost> Cost(ref SystemState state, Entity squad)
        {
            return SystemAPI.HasBuffer<ResourceCost>(squad)
                ? SystemAPI.GetBuffer<ResourceCost>(squad)
                : default;
        }

        /// <summary>Steps the member in progress; false once the reinforcing is over.</summary>
        private bool Advance(ref SystemState state, Entity squad, ref Reinforcing reinforcing,
            DynamicBuffer<SquadMember> members, ref Health health, float deltaTime)
        {
            var share = _rules.ReinforceShare(members.Length);
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var stock = SystemAPI.GetBuffer<ResourceStock>(reinforcing.Payer);
            if (!new SquadRoster(members).AnyDead() || !_squadPoints.Reaches(reinforcing.Point, here))
            {
                if (reinforcing.Paid)
                {
                    SquadOrderRule.Refund(Cost(ref state, squad), stock, share);
                }

                return false;
            }

            if (!reinforcing.Paid)
            {
                reinforcing.Paid = SquadOrderRule.Pay(Cost(ref state, squad), stock, share);
                if (!reinforcing.Paid)
                {
                    return false;
                }
            }

            reinforcing.Elapsed += deltaTime;
            if (reinforcing.Elapsed >= _rules.ReinforceTime)
            {
                new SquadRoster(members).Revive();
                health.Current = new SquadRoster(members).Total();
                reinforcing.Elapsed = 0f;
                reinforcing.Paid = false;
            }

            return true;
        }
    }
}
