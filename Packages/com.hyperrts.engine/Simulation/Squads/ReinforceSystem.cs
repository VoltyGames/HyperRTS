using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Handles the Reinforce command for the commanded or selected squads that have dead members, stand within range of
    /// a friendly working <see cref="ReinforcePoint"/> and can pay for a member. Reinforcing brings the dead back one at
    /// a time at full health, in place in the member list, each taking <see cref="SquadRules.ReinforceTime"/> and
    /// <see cref="SquadRules.ReinforceCost"/> of its slice of the squad's price, paid when its turn starts. It ends when
    /// no one is dead, the squad leaves the point's range or the player can't pay for the next; a member in progress is
    /// refunded.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct ReinforceSystem : ISystem
    {
        private CommandReader _commands;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<SquadMember, Selected, Faction>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Reinforce), selected);
            state.RequireForUpdate<SquadRules>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var rules = SystemAPI.GetSingleton<SquadRules>();
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            foreach (var each in _commands.Read(ref state))
            {
                foreach (var squad in _commands.Subjects(ref state, each))
                {
                    Begin(ref state, squad, each.Player, rules, relations);
                }
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (reinforcing, enabled, members, health, entity) in SystemAPI
                         .Query<RefRW<Reinforcing>, EnabledRefRW<Reinforcing>, DynamicBuffer<SquadMember>, RefRW<Health>>()
                         .WithEntityAccess())
            {
                if (!Advance(ref state, entity, ref reinforcing.ValueRW, members, ref health.ValueRW, rules, deltaTime))
                {
                    enabled.ValueRW = false;
                }
            }
        }

        private void Begin(ref SystemState state, Entity squad, Entity player, in SquadRules rules,
            in FactionRelations relations)
        {
            if (!SystemAPI.HasComponent<Reinforcing>(squad) || SystemAPI.IsComponentEnabled<Reinforcing>(squad))
            {
                return;
            }

            var members = SystemAPI.GetBuffer<SquadMember>(squad);
            if (!SquadMember.AnyDead(members) || !CanPay(ref state, squad, player, rules.ReinforceCost / members.Length))
            {
                return;
            }

            var point = FindPoint(ref state, squad, relations);
            if (point != Entity.Null)
            {
                SystemAPI.SetComponent(squad, new Reinforcing { Point = point, Payer = player });
                SystemAPI.SetComponentEnabled<Reinforcing>(squad, true);
            }
        }

        /// <summary>Whether the player can pay <paramref name="share"/> of the squad's price; a squad with none is free.</summary>
        private bool CanPay(ref SystemState state, Entity squad, Entity player, float share) =>
            !SystemAPI.HasBuffer<ResourceCost>(squad)
            || ResourceMath.CanAfford(SystemAPI.GetBuffer<ResourceStock>(player), SystemAPI.GetBuffer<ResourceCost>(squad), share);

        private bool Pay(ref SystemState state, Entity squad, Entity player, float share) =>
            !SystemAPI.HasBuffer<ResourceCost>(squad)
            || ResourceMath.TrySpend(SystemAPI.GetBuffer<ResourceStock>(player), SystemAPI.GetBuffer<ResourceCost>(squad), share);

        /// <summary>The nearest friendly working point the squad stands within the range of, or null.</summary>
        private Entity FindPoint(ref SystemState state, Entity squad, in FactionRelations relations)
        {
            var faction = SystemAPI.GetComponent<Faction>(squad).Value;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var found = Entity.Null;
            var best = float.MaxValue;
            foreach (var (point, owner, transform, entity) in SystemAPI
                         .Query<RefRO<ReinforcePoint>, RefRO<Faction>, RefRO<LocalTransform>>()
                         .WithNone<Dead, Offline>().WithEntityAccess())
            {
                var distance = math.distancesq(here.xz, transform.ValueRO.Position.xz);
                var inRange = distance <= point.ValueRO.Range * point.ValueRO.Range;
                if (inRange && distance < best && relations.IsAllied(faction, owner.ValueRO.Value))
                {
                    best = distance;
                    found = entity;
                }
            }

            return found;
        }

        /// <summary>Steps the member in progress; false once the reinforcing is over.</summary>
        private bool Advance(ref SystemState state, Entity squad, ref Reinforcing reinforcing,
            DynamicBuffer<SquadMember> members, ref Health health, in SquadRules rules, float deltaTime)
        {
            var share = rules.ReinforceCost / members.Length;
            if (!SquadMember.AnyDead(members) || !IsWorking(ref state, squad, reinforcing.Point))
            {
                Refund(ref state, squad, reinforcing, share);
                return false;
            }

            if (!reinforcing.Paid)
            {
                reinforcing.Paid = Pay(ref state, squad, reinforcing.Payer, share);
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

        /// <summary>Whether the point still stands and works and the squad is still within its range.</summary>
        private bool IsWorking(ref SystemState state, Entity squad, Entity point)
        {
            var dead = SystemAPI.HasComponent<Dead>(point) && SystemAPI.IsComponentEnabled<Dead>(point);
            var offline = SystemAPI.HasComponent<Offline>(point) && SystemAPI.IsComponentEnabled<Offline>(point);
            if (!SystemAPI.HasComponent<ReinforcePoint>(point) || dead || offline)
            {
                return false;
            }

            var range = SystemAPI.GetComponent<ReinforcePoint>(point).Range;
            var gap = SystemAPI.GetComponent<LocalTransform>(squad).Position.xz
                      - SystemAPI.GetComponent<LocalTransform>(point).Position.xz;
            return math.lengthsq(gap) <= range * range;
        }

        private void Refund(ref SystemState state, Entity squad, in Reinforcing reinforcing, float share)
        {
            if (reinforcing.Paid && SystemAPI.HasBuffer<ResourceCost>(squad))
            {
                ResourceMath.Refund(SystemAPI.GetBuffer<ResourceStock>(reinforcing.Payer),
                    SystemAPI.GetBuffer<ResourceCost>(squad), share);
            }
        }
    }
}
