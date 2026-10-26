using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Validates and leashes <see cref="AttackTarget"/>s, chases targets out of weapon range, backs off from ordered ones
    /// inside the minimum range and halts once in range. A rooted <see cref="Deployable"/> fights like a building.
    /// Also keeps <see cref="CombatStance.ReturnPoint"/> on idle, unengaged units.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(TargetAcquisitionSystem))]
    public partial struct EngagementSystem : ISystem
    {
        /// <summary>Chasers re-target their move only once the target has drifted this far from it.</summary>
        public const float RepathDistance = 1f;

        private TargetLookup _targets;
        private EntityQuery _fogQuery;
        private ComponentLookup<ActiveOrder> _orders;
        private ComponentLookup<MoveDestination> _moves;
        private ComponentLookup<Ammo> _ammo;
        private ComponentLookup<Deployable> _deployables;
        private BufferLookup<WeaponMount> _mounts;
        private BufferLookup<ArmorModifier> _armor;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _orders = state.GetComponentLookup<ActiveOrder>(true);
            _moves = state.GetComponentLookup<MoveDestination>();
            _ammo = state.GetComponentLookup<Ammo>(true);
            _deployables = state.GetComponentLookup<Deployable>(true);
            _mounts = state.GetBufferLookup<WeaponMount>(true);
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);
            _orders.Update(ref state);
            _moves.Update(ref state);
            _ammo.Update(ref state);
            _deployables.Update(ref state);
            _mounts.Update(ref state);
            _armor.Update(ref state);

            var traits = SystemAPI.GetSingleton<OrderTraitTable>();

            new EngageJob
            {
                Targets = _targets,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Orders = _orders,
                Traits = traits,
                Moves = _moves,
                AmmoLookup = _ammo,
                Deployables = _deployables,
                Mounts = _mounts,
                Armor = _armor,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct EngageJob : IJobEntity
        {
            public TargetLookup Targets;
            public FactionRelations Relations;
            [ReadOnly] public ComponentLookup<ActiveOrder> Orders;
            public OrderTraitTable Traits;
            [ReadOnly] public ComponentLookup<Ammo> AmmoLookup;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            [ReadOnly] public BufferLookup<WeaponMount> Mounts;
            [ReadOnly] public BufferLookup<ArmorModifier> Armor;

            // Each entity writes only its own MoveDestination.
            [NativeDisableParallelForRestriction] public ComponentLookup<MoveDestination> Moves;

            private void Execute(Entity entity, in LocalTransform transform, in Weapon weapon, in VisionRange vision,
                in Faction faction, ref CombatStance stance, ref AttackTarget attack, EnabledRefRW<AttackTarget> attacking)
            {
                var order = CurrentOrder(entity);
                if (!attacking.ValueRO)
                {
                    // Attack-movers carry their post along, so a defensive leash is measured from where they engage.
                    var standing = order.Type == OrderType.None && !IsMoving(entity);
                    if (standing || Traits.Has(order.Type, OrderTraits.EngagesWhileMoving))
                    {
                        stance.ReturnPoint = transform.Position;
                    }

                    return;
                }

                // An ordered attack chases indefinitely; the stance only governs targets the unit picked itself.
                var target = attack.Value;
                var ordered = order.Type == OrderType.Attack && order.Target == target;
                var reach = Reach(entity, weapon, target, out var floor);
                if (Loses(entity, weapon, faction, target, ordered))
                {
                    Release(entity, order, stance, ref attack, attacking);
                    return;
                }

                var gap = Targets.Gap(entity, transform.Position, target);
                if (!ordered && ShouldLeash(entity, transform.Position, weapon, vision, stance, target, gap, reach))
                {
                    Release(entity, order, stance, ref attack, attacking);
                    return;
                }

                attack.Gap = gap;
                if (!ordered && attack.Gap < floor)
                {
                    Release(entity, order, stance, ref attack, attacking);
                    return;
                }

                if (ordered)
                {
                    stance.ReturnPoint = transform.Position;
                }

                Engage(entity, transform.Position, target, ref attack, floor, reach);
            }

            /// <summary>Halts in range, chases out of it, and backs an ordered unit off a target inside its minimum range.</summary>
            private void Engage(Entity entity, float3 position, Entity target, ref AttackTarget attack, float floor,
                float reach)
            {
                var middle = 0.5f * (floor + reach);
                attack.BackingOff = BacksOff(entity, attack, floor, middle);
                attack.InRange = !attack.BackingOff && attack.Gap >= floor && attack.Gap <= reach;
                if (attack.BackingOff)
                {
                    BackOff(entity, position, target, middle);
                }
                else if (attack.Gap <= reach)
                {
                    Halt(entity);
                }
                else
                {
                    Chase(entity, Targets.Position(target));
                }
            }

            /// <summary>Gone, hidden, out of the weapon's reach or out of ammo.</summary>
            private bool Loses(Entity entity, in Weapon weapon, in Faction faction, Entity target, bool ordered)
            {
                // Only orders point units at scenery, so an ordered target need only be attackable.
                var keep = ordered
                    ? Targets.IsAttackable(target, faction.Value, Relations, weapon.Targets)
                    : Targets.IsValidTarget(target, faction.Value, Relations, weapon.Targets);
                if (!keep)
                {
                    return true;
                }

                return Ammo.IsEmpty(AmmoLookup, entity);
            }

            /// <summary><paramref name="gap"/> is the edge distance from <paramref name="position"/>.</summary>
            private bool ShouldLeash(Entity entity, float3 position, in Weapon weapon, in VisionRange vision,
                in CombatStance stance, Entity target, float gap, float reach)
            {
                if (stance.Value == Stance.HoldPosition || !Mobile(entity))
                {
                    return gap > reach;
                }

                // Defensive units measure from their post, so a fleeing target can't drag them away.
                var origin = stance.Value == Stance.Defensive ? stance.ReturnPoint : position;
                var leash = CombatMath.AcquireRange(weapon, vision.Value) * weapon.LeashFactor;
                return Targets.Gap(entity, origin, target) > leash;
            }

            /// <summary>Longest range and shortest minimum of the primary and the ready shared-target mounts that can hit it.</summary>
            private float Reach(Entity entity, in Weapon weapon, Entity target, out float floor)
            {
                var reach = weapon.Range;
                floor = weapon.MinRange;
                if (!Mounts.TryGetBuffer(entity, out var mounts))
                {
                    return reach;
                }

                foreach (var mount in mounts)
                {
                    if (JoinsAttack(mount, target))
                    {
                        reach = math.max(reach, mount.Weapon.Range);
                        floor = math.min(floor, mount.Weapon.MinRange);
                    }
                }

                return reach;
            }

            private bool JoinsAttack(in WeaponMount mount, Entity target)
            {
                if (mount.Independent || !mount.Ready)
                {
                    return false;
                }

                return Targets.CanHurt(target, mount.Weapon, Armor);
            }

            private void Release(Entity entity, in Order order, in CombatStance stance, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking)
            {
                attack.InRange = false;
                attack.BackingOff = false;
                attacking.ValueRW = false;

                if (!Mobile(entity))
                {
                    return;
                }

                // Only idle defenders head home. Moves and escorts halt, and their order systems pick up again.
                if (order.Type == OrderType.None && stance.Value == Stance.Defensive)
                {
                    MoveTo(entity, stance.ReturnPoint);
                }
                else
                {
                    Halt(entity);
                }
            }

            private void Chase(Entity entity, float3 targetPosition)
            {
                if (Mobile(entity))
                {
                    ReachMath.MoveTo(ref Moves.GetRefRW(entity).ValueRW, Moves.GetEnabledRefRW<MoveDestination>(entity),
                        targetPosition, RepathDistance);
                }
            }

            /// <summary>Starts inside the minimum range if the unit can move, and keeps on until mid range or arrival.</summary>
            private bool BacksOff(Entity entity, in AttackTarget attack, float floor, float middle)
            {
                if (attack.BackingOff)
                {
                    return attack.Gap < middle && IsMoving(entity);
                }

                return attack.Gap < floor && Mobile(entity);
            }

            /// <summary>Retreats straight away from the target to <paramref name="gap"/> edge to edge.</summary>
            private void BackOff(Entity entity, float3 position, Entity target, float gap)
            {
                var from = Targets.Position(target);
                var level = new float3(position.x - from.x, 0f, position.z - from.z);
                var away = math.normalizesafe(level, new float3(0f, 0f, -1f));
                var goal = from + away * (gap + Targets.Radius(entity) + Targets.Radius(target));
                ReachMath.MoveTo(ref Moves.GetRefRW(entity).ValueRW, Moves.GetEnabledRefRW<MoveDestination>(entity), goal,
                    RepathDistance);
            }

            private void Halt(Entity entity)
            {
                if (Moves.HasComponent(entity))
                {
                    Moves.SetComponentEnabled(entity, false);
                }
            }

            private void MoveTo(Entity entity, float3 goal) =>
                ReachMath.MoveTo(ref Moves.GetRefRW(entity).ValueRW, Moves.GetEnabledRefRW<MoveDestination>(entity), goal);

            private bool IsMoving(Entity entity) => Moves.HasEnabled(entity);

            private bool Mobile(Entity entity) => Moves.HasComponent(entity) && !Deployable.IsImmobile(Deployables, entity);

            private Order CurrentOrder(Entity entity) => Orders.HasEnabled(entity) ? Orders[entity].Value : default;
        }
    }
}
