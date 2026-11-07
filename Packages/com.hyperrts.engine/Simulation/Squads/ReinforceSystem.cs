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
    /// Handles the Reinforce command: squads with dead members in range of a friendly working <see
    /// cref="ReinforcePoint"/> bring them back one at a time, paying <see cref="SquadRules.ReinforceCost"/> each. It
    /// ends when no one is dead, the squad leaves range or the player can't pay.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct ReinforceSystem : ISystem
    {
        private CommandReader _commands;
        private EntityQuery _points;

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
            var rules = SystemAPI.GetSingleton<SquadRules>();
            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            var points = SquadPoints.From<ReinforcePoint>(_points);
            foreach (var each in _commands.Read(ref state))
            {
                foreach (var squad in _commands.Subjects(ref state, each))
                {
                    Begin(ref state, squad, each.Player, rules, relations, points);
                }
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (reinforcing, enabled, members, health, entity) in SystemAPI
                         .Query<RefRW<Reinforcing>, EnabledRefRW<Reinforcing>, DynamicBuffer<SquadMember>, RefRW<Health>>()
                         .WithEntityAccess())
            {
                if (!Advance(ref state, entity, ref reinforcing.ValueRW, members, ref health.ValueRW, rules, points,
                        deltaTime))
                {
                    enabled.ValueRW = false;
                }
            }
        }

        private void Begin(ref SystemState state, Entity squad, Entity player, in SquadRules rules,
            in OwnerRelations relations, in SquadPoints points)
        {
            if (!SystemAPI.HasComponent<Reinforcing>(squad) || SystemAPI.IsComponentEnabled<Reinforcing>(squad))
            {
                return;
            }

            var owner = SystemAPI.GetComponent<Owner>(squad).Value;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var point = points.NearestCovering(here, owner, relations);
            var refusal = SquadOrderRule.FindReinforceRefusal(SystemAPI.GetBuffer<SquadMember>(squad),
                point != Entity.Null, rules, Cost(ref state, squad), SystemAPI.GetBuffer<ResourceStock>(player));
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
            DynamicBuffer<SquadMember> members, ref Health health, in SquadRules rules, in SquadPoints points,
            float deltaTime)
        {
            var share = rules.ReinforceShare(members.Length);
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var stock = SystemAPI.GetBuffer<ResourceStock>(reinforcing.Payer);
            if (!SquadMember.AnyDead(members) || !points.Reaches(reinforcing.Point, here))
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
            if (reinforcing.Elapsed >= rules.ReinforceTime)
            {
                SquadMember.Revive(members);
                health.Current = SquadMember.Total(members);
                reinforcing.Elapsed = 0f;
                reinforcing.Paid = false;
            }

            return true;
        }
    }
}
