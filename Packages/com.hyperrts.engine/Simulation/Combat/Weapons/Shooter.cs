using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision.Detection;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons;

/// <summary>
/// Fires a weapon from a job: the fire cue, an instant hit or a launched <see cref="Projectile"/>, and the stealth
/// reveal. Use from one thread.
/// </summary>
public struct Shooter
{
    [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
    private PrefabSpawner _spawner;
    private ComponentLookup<Stealth> _stealth;
    [ReadOnly] private ComponentLookup<Deployable> _deployables;
    private FiringLookup _origins;
    private BufferLookup<WeaponPose> _poses;
    private ComponentLookup<Projectile> _projectiles;
    private EventWriter<DamageEvent> _damage;
    private CueWriter _cues;
    private EntityCommandBuffer _ecb;
    [ReadOnly] private SightBlockers _blockers;

    public Shooter(ref SystemState state) : this()
    {
        _transforms = state.GetComponentLookup<LocalTransform>(true);
        _spawner = new PrefabSpawner(ref state);
        _stealth = state.GetComponentLookup<Stealth>();
        _deployables = state.GetComponentLookup<Deployable>(true);
        _origins = new FiringLookup(ref state);
        _poses = state.GetBufferLookup<WeaponPose>();
        _projectiles = state.GetComponentLookup<Projectile>();
        _damage = new EventWriter<DamageEvent>(ref state);
        _cues = new CueWriter(ref state);
    }

    /// <summary>Call each update before scheduling, with this frame's buffer and the system's blockers query.</summary>
    public void Update(ref SystemState state, EntityCommandBuffer ecb, EntityQuery blockers)
    {
        _transforms.Update(ref state);
        _spawner.Update(ref state);
        _stealth.Update(ref state);
        _deployables.Update(ref state);
        _origins.Update(ref state);
        _poses.Update(ref state);
        _projectiles.Update(ref state);
        _damage.Update(ref state);
        _cues.Update(ref state);
        _ecb = ecb;
        Read(blockers, out _blockers);
    }

    // Through out, because Burst only lets the [ReadOnly] field be filled that way.
    private static void Read(EntityQuery query, out SightBlockers blockers) =>
        blockers = query.GetSingleton<SightBlockers>();

    /// <summary>
    /// The fire step every weapon takes, ending in one volley of a round per carrier with one reveal, one shot
    /// counted and one fire cue.
    /// </summary>
    public bool TryFire(Entity shooter, ref WeaponMount mount, byte pose, in ShotAim aim, byte owner)
    {
        if (Deployable.Holds(_deployables, shooter, mount.Weapon.NeedsDeploy) || OutsideArc(shooter, mount.Weapon, aim.Point))
        {
            mount.Cycle.EndSalvo(mount.Weapon);
            return false;
        }

        if (mount.Cycle.CoolingDown || !Aimed(shooter, mount.Weapon, pose, aim.Point))
        {
            return false;
        }

        Fire(shooter, ref mount, pose, aim, owner);
        mount.Cycle.StartCooldown(mount.Weapon);
        return true;
    }

    /// <summary>Whether <paramref name="target"/> is a projectile still flying inside the weapon's range.</summary>
    public bool CanIntercept(Entity shooter, in Weapon weapon, Entity target)
    {
        if (!_projectiles.TryGetComponent(target, out var projectile) || projectile.Intercepted)
        {
            return false;
        }

        return weapon.ReachesInFlight(_transforms[shooter].Position, _transforms[target].Position);
    }

    private bool OutsideArc(Entity shooter, in Weapon weapon, float3 point) =>
        weapon.FiringArc > 0f && !weapon.InFiringArc(_deployables, shooter, _transforms[shooter], point);

    /// <summary>A hull-fixed weapon fires as the hull faces; a turret once its pose points at the target.</summary>
    private bool Aimed(Entity shooter, in Weapon weapon, byte pose, float3 point)
    {
        if (!weapon.Aim.Turreted)
        {
            return true;
        }

        var aimed = _poses[shooter][pose];
        var aim = weapon.Aim.TargetPoint(point);
        return weapon.Aim.Aligned(aimed, weapon.Aim.Angles(_transforms[shooter], aimed, aim));
    }

    /// <summary>
    /// Fires a volley at <see cref="WeaponPose"/> index <paramref name="pose"/>: the reveal, the shot count and the
    /// fire cue happen once, the hit or launch once per round.
    /// </summary>
    private void Fire(Entity shooter, ref WeaponMount mount, byte pose, in ShotAim aim, byte owner)
    {
        if (_stealth.TryGetRefRW(shooter, out var stealth))
        {
            stealth.ValueRW.RevealTimer = stealth.ValueRO.RevealDuration;
        }

        var poses = _poses[shooter];
        var fired = poses[pose];
        fired.CountShot();
        poses[pose] = fired;
        var firing = _origins.Start(shooter, owner, pose, fired, aim.Point);
        for (var round = 0; round < mount.Carriers; round++)
        {
            var origin = _origins.NextOrigin(firing, mount.Weapon, ref mount.Cycle);
            if (round == 0)
            {
                PlayFire(firing, mount.Weapon, origin, aim.Point);
            }

            Shoot(firing, mount.Weapon, origin, round, aim);
        }
    }

    private void Shoot(in Firing firing, in Weapon weapon, in ShotOrigin origin, int round, in ShotAim aim)
    {
        if (weapon.FiresRounds)
        {
            var aimed = aim.Target;
            var aimedAt = aim.Point;
            Obstruct(firing, weapon, origin, ref aimed, ref aimedAt);
            Launch(firing, weapon, origin, round, aimed, aimedAt);
        }
        else if (weapon.Role == WeaponRole.Interceptor)
        {
            Intercept(firing, weapon, origin, aim);
        }
        else
        {
            Strike(firing, weapon, origin, round == 0, aim);
        }
    }

    /// <summary>An instant hit; the first round of a volley also writes the impact cue.</summary>
    private void Strike(in Firing firing, in Weapon weapon, in ShotOrigin origin, bool cue, in ShotAim aim)
    {
        var hit = weapon.Damage.Hit(firing.Shooter, firing.Owner, origin.Hit, aim.Target, aim.Point, weapon.Targets);
        _damage.Add(hit);
        if (cue)
        {
            AddImpact(firing, hit, origin.Hit, aim.Point);
        }
    }

    private void AddImpact(in Firing firing, in DamageEvent hit, float3 origin, float3 targetPosition)
    {
        var along = math.normalizesafe(targetPosition - origin, firing.Place.Forward());
        hit.AddImpact(ref _cues, _cues.TypeIdFor(firing.Shooter, CueSlot.Impact), along);
    }

    /// <summary>A shot at a projectile: a seeded roll that downs it, then the impact cue where it dies.</summary>
    private void Intercept(in Firing firing, in Weapon weapon, in ShotOrigin origin, in ShotAim aim)
    {
        // Seeded by shooter, target and shot count, so the same fight rolls the same.
        var seed = math.hash(new uint4((uint)firing.Shooter.Index, (uint)aim.Target.Index, (uint)aim.Target.Version,
            firing.Pose.Shots));
        if (!weapon.Kills(seed))
        {
            return;
        }

        var projectile = _projectiles[aim.Target];
        projectile.Intercepted = true;
        _projectiles[aim.Target] = projectile;
        _ecb.DestroyEntity(aim.Target);
        var hit = weapon.Damage.Hit(firing.Shooter, firing.Owner, origin.Hit, aim.Target, aim.Point, weapon.Targets);
        AddImpact(firing, hit, origin.Hit, aim.Point);
    }

    /// <summary>
    /// Turns a direct-fire round onto the solid block that stands between its launch point and the target. The
    /// ground turns none: its shooter fires only with a line across it.
    /// </summary>
    private void Obstruct(in Firing firing, in Weapon weapon, in ShotOrigin origin, ref Entity target,
        ref float3 targetPosition)
    {
        if (!weapon.NeedsLineOfFire)
        {
            return;
        }

        var aim = weapon.Aim.TargetPoint(targetPosition);
        if (_blockers.TryFindBlock(origin.Launch, aim, firing.Housing, target, out var contact, out var obstacle))
        {
            target = obstacle;
            targetPosition = contact;
        }
    }

    /// <summary>
    /// The fire cue plays from the muzzle along the posed barrel, so a raised launcher's blast leaves its tubes;
    /// from a port, or with no muzzle, it faces the target level, the way the shooter turned.
    /// </summary>
    private void PlayFire(in Firing firing, in Weapon weapon, in ShotOrigin origin, float3 targetPosition)
    {
        if (origin.AtMuzzle && !firing.Ported)
        {
            var barrel = weapon.Aim.BarrelDirection(firing.Place, firing.Pose);
            _cues.PlayAt(firing.Shooter, CueSlot.Fire, origin.Launch, barrel, firing.Owner, firing.PoseIndex);
            return;
        }

        var toward = targetPosition - firing.Place.Position;
        toward.y = 0f;
        var direction = math.normalizesafe(toward, firing.Place.Forward());
        if (origin.AtMuzzle)
        {
            _cues.PlayAt(firing.Shooter, CueSlot.Fire, origin.Launch, direction, firing.Owner, firing.PoseIndex);
            return;
        }

        _cues.Play(firing.Shooter, CueSlot.Fire, firing.Place, direction, firing.Owner, firing.PoseIndex);
    }

    private void Launch(in Firing firing, in Weapon weapon, in ShotOrigin origin, int round, Entity target,
        float3 targetPosition)
    {
        var start = origin.Launch;
        var seed = math.hash(new uint4((uint)firing.Shooter.Index, (uint)target.Index, firing.Pose.Shots, (uint)round));
        var aim = weapon.Scatter(weapon.Aim.TargetPoint(targetPosition), seed);

        // Smoke thick enough to hide the target breaks a guided round's lock; it flies on to the aim point.
        var locked = weapon.KeepsLock && _blockers.Haze(start, aim) < 1f;
        var hit = weapon.Damage.Hit(firing.Shooter, firing.Owner, start, locked ? target : Entity.Null, aim, weapon.Targets);
        var flight = weapon.Launched(hit, _cues.TypeIdFor(firing.Shooter, CueSlot.Impact), start, aim);
        var heading = flight.Arcs ? flight.Heading() : aim - start;
        var projectile = _spawner.Spawn(_ecb, weapon.ProjectilePrefab, start,
            quaternion.LookRotationSafe(heading, math.up()), firing.Owner);
        _ecb.AddComponent(projectile, flight);
    }
}
