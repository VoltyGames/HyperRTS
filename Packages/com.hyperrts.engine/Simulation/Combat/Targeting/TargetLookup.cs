using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Read-only view of other entities for combat jobs: alive, hostile, detected, hittable, where and how big.
    /// </summary>
    public struct TargetLookup
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<Owner> _owners;
        [ReadOnly] private ComponentLookup<NavAgent> _agents;
        [ReadOnly] private ComponentLookup<NavObstacle> _obstacles;
        [ReadOnly] private ComponentLookup<Inside> _inside;
        private ConcealmentLookup _concealment;
        [ReadOnly] private ComponentLookup<Destructible> _destructibles;
        [ReadOnly] private ComponentLookup<PassengerRange> _passengerRange;
        [ReadOnly] private ComponentLookup<VisionRange> _vision;
        [ReadOnly] private ComponentLookup<Deployable> _deployables;
        [ReadOnly] private SightBlockers _blockers;
        private DamageFactors _damage;
        [ReadOnly] private FogOfWar _fog;
        private SightRules _sight;

        public TargetLookup(ref SystemState state)
        {
            // Zeroes the [ReadOnly] blockers too: Burst refuses a direct store to them.
            this = default;
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _health = state.GetComponentLookup<Health>(true);
            _owners = state.GetComponentLookup<Owner>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _inside = state.GetComponentLookup<Inside>(true);
            _concealment = new ConcealmentLookup(ref state);
            _destructibles = state.GetComponentLookup<Destructible>(true);
            _passengerRange = state.GetComponentLookup<PassengerRange>(true);
            _vision = state.GetComponentLookup<VisionRange>(true);
            _deployables = state.GetComponentLookup<Deployable>(true);
            _damage = new DamageFactors(ref state);
        }

        /// <summary><paramref name="queries"/> are the system's, created once in OnCreate.</summary>
        public void Update(ref SystemState state, in TargetQueries queries)
        {
            _transforms.Update(ref state);
            _health.Update(ref state);
            _owners.Update(ref state);
            _agents.Update(ref state);
            _obstacles.Update(ref state);
            _inside.Update(ref state);
            _concealment.Update(ref state);
            _destructibles.Update(ref state);
            _passengerRange.Update(ref state);
            _vision.Update(ref state);
            _deployables.Update(ref state);
            _damage.Update(ref state);
            // Burst only lets the [ReadOnly] singletons be filled through out.
            queries.Fog.TryGetSingleton(out _fog);
            queries.Sight.TryGetSingleton(out _sight);
            queries.Blockers.TryGetSingleton(out _blockers);
        }

        /// <summary>False once destroyed or at zero health (dying entities linger until the frame ends).</summary>
        public bool IsAlive(Entity entity) => Health.IsAlive(_health, entity);

        /// <summary>Alive, hostile, not tucked inside a container and not hidden from the attacker by stealth.</summary>
        public bool IsValidTarget(Entity target, byte attackerOwner, in OwnerRelations relations)
        {
            if (!IsAlive(target) || IsInside(target) || !_owners.TryGetComponent(target, out var owner))
            {
                return false;
            }

            var hostile = relations.IsHostile(attackerOwner, owner.Value);
            return hostile && !IsHiddenByConcealment(target, relations.GetTeam(attackerOwner));
        }

        /// <summary>
        /// What an explicit attack order may target: a valid hostile target, or any living <see cref="Destructible"/>
        /// map scenery the weapon can hit. Units never pick scenery on their own.
        /// </summary>
        public bool IsAttackable(Entity target, byte attackerOwner, in OwnerRelations relations, WeaponTargets targets)
        {
            if (IsValidTarget(target, attackerOwner, relations, targets))
            {
                return true;
            }

            var scenery = _destructibles.HasComponent(target) && IsAlive(target) && !IsInside(target);
            return scenery && CanHit(target, targets);
        }

        /// <summary>A valid target (see above) that a weapon reaching <paramref name="targets"/> can hit.</summary>
        public bool IsValidTarget(Entity target, byte attackerOwner, in OwnerRelations relations,
            WeaponTargets targets) =>
            IsValidTarget(target, attackerOwner, relations) && CanHit(target, targets);

        public bool CanHit(Entity target, WeaponTargets targets)
        {
            return Weapon.CanHit(targets, NavAgent.GetLayer(_agents, target));
        }

        /// <summary>The weapon can hit the target and its damage is worth it (<see cref="Weapon.Affects"/>).</summary>
        public bool CanHurt(Entity target, in Weapon weapon) => CanHit(target, weapon.Targets) && Affects(target, weapon);

        /// <summary>The target's armor lets the weapon's damage type through at <see cref="Weapon.MinEffect"/> or more.</summary>
        public bool Affects(Entity target, in Weapon weapon) =>
            weapon.MinEffect <= 0f || _damage.Armor(target, weapon.Damage.Type) >= weapon.MinEffect;

        /// <summary>Stealthed or concealed from the team (see <see cref="FogOfWar.IsHiddenByConcealment"/>).</summary>
        public bool IsHiddenByConcealment(Entity target, byte team)
        {
            // Concealment first, so candidates in the open skip the transform lookup.
            var concealment = _concealment.Find(target);
            return concealment != Concealment.None && _fog.IsHiddenByConcealment(team, Position(target), concealment);
        }

        /// <summary>
        /// In the team's sight right now and not cloaked from it: all a unit may pick on its own. Callers require the
        /// fog grid, which fog-off maps fill as all-visible.
        /// </summary>
        public bool IsSpotted(Entity target, float3 position, byte team) =>
            _fog.CanSee(team, position, _concealment.Find(target), IsTall(target));

        /// <summary>As tall as a vehicle or a building, so seen over low cover (see <see cref="TallTarget"/>).</summary>
        public bool IsTall(Entity entity) => _concealment.IsTall(entity);

        /// <summary>
        /// How far the weapon of <paramref name="shooter"/>, standing at <paramref name="position"/>, reaches
        /// <paramref name="target"/>: further from a garrison that grants it, and for direct fire from above the target.
        /// </summary>
        public float Reach(Entity shooter, float3 position, in Weapon weapon, Entity target)
        {
            var bonus = GarrisonBonus(shooter);
            if (weapon.NeedsLineOfFire)
            {
                bonus += _sight.RangeBonus(position.y - Position(target).y);
            }

            return weapon.Range * (1f + bonus);
        }

        /// <summary>
        /// Furthest the weapon could <see cref="Reach"/> any target: from a garrison, and for direct fire with the
        /// most high ground can add.
        /// </summary>
        public float ScanReach(Entity shooter, in Weapon weapon)
        {
            var bonus = GarrisonBonus(shooter);
            if (weapon.NeedsLineOfFire)
            {
                bonus += _sight.MaxRange;
            }

            return weapon.Range * (1f + bonus);
        }

        /// <summary>Range share a passenger gains from the container it fires out of.</summary>
        private float GarrisonBonus(Entity shooter) =>
            IsInside(shooter) && _passengerRange.TryGetComponent(_inside[shooter].Container, out var range)
                ? range.Bonus
                : 0f;

        /// <summary>
        /// Whether the weapon may shoot at the target from <paramref name="position"/>: lobbed fire always may, direct
        /// fire only with a line from the shooter's eye to the target's top clear of the ground and solid blocks.
        /// </summary>
        public bool HasLineOfFire(Entity shooter, float3 position, in Weapon weapon, Entity target) =>
            !weapon.NeedsLineOfFire || !IsObstructed(shooter, position, target);

        /// <summary>
        /// Whether the weapon may fire now: rounds fire into what's in the way, other direct fire waits for a clear
        /// line (<see cref="HasLineOfFire"/>).
        /// </summary>
        public bool CanFireAt(Entity shooter, float3 position, in Weapon weapon, Entity target) =>
            weapon.FiresRounds || HasLineOfFire(shooter, position, weapon, target);

        /// <summary>Whether something solid stands between the shooter's eye and the target's top.</summary>
        private bool IsObstructed(Entity shooter, float3 position, Entity target)
        {
            if (!_blockers.IsCreated)
            {
                return false;
            }

            var holder = Holder(shooter);
            _vision.TryGetComponent(shooter, out var vision);
            var from = position + new float3(0f, vision.EyeHeight, 0f);
            var to = Position(target) + new float3(0f, SightLines.TargetTop(IsTall(target)), 0f);
            return _blockers.TryFindObstruction(from, to, holder, target, out _, out _);
        }

        /// <summary>Whether the target sits between the weapon's minimum range and its <see cref="Reach"/>.</summary>
        public bool Reaches(Entity shooter, float3 position, in Weapon weapon, Entity target)
        {
            return weapon.Band(Reach(shooter, position, weapon, target)).Contains(Gap(shooter, position, target));
        }

        /// <summary>Whether the target lies within the weapon's firing arc, always so for an all-round weapon.</summary>
        public bool InFiringArc(Entity shooter, in Weapon weapon, Entity target) =>
            weapon.InFiringArc(_deployables, shooter, _transforms[shooter], Position(target));

        public float3 Position(Entity entity) => _transforms[entity].Position;

        /// <summary>Edge to edge distance with <paramref name="entity"/> standing at <paramref name="position"/>.</summary>
        public float Gap(Entity entity, float3 position, Entity target) =>
            EntityRadius.EdgeDistance(position, Radius(entity), Position(target), Radius(target));

        /// <summary>Passengers take their container's footprint, so they fire from its edge.</summary>
        public float Radius(Entity entity) =>
            EntityRadius.Get(Holder(entity), _agents, _obstacles);

        /// <summary>Tucked inside a transport or garrison: hidden and untargetable.</summary>
        public bool IsInside(Entity entity) => _inside.HasEnabled(entity);

        /// <summary>The container a passenger sits in, otherwise the entity itself.</summary>
        public Entity Holder(Entity entity) => IsInside(entity) ? _inside[entity].Container : entity;
    }
}
